using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace frontida4baby.Web.Controllers;

public class SubscriptionController : Controller
{
    private readonly ISubscriptionService _subscription;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserEmailService _userEmail;
    private readonly IAppLogService _appLog;
    private readonly IConfiguration _config;

    public SubscriptionController(
        ISubscriptionService subscription,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserEmailService userEmail,
        IAppLogService appLog,
        IConfiguration config)
    {
        _subscription = subscription;
        _db = db;
        _userManager = userManager;
        _userEmail = userEmail;
        _appLog = appLog;
        _config = config;
    }

    [HttpGet("/subscription")]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        SubscriptionPlan plan = SubscriptionPlan.Free;
        if (userId != null) plan = await _subscription.GetPlanAsync(userId);
        ViewBag.CurrentPlan   = plan;
        ViewBag.StripeEnabled = _config.GetValue<bool>("Stripe:Enabled");
        return View();
    }

    [HttpPost("/subscription/checkout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout()
    {
        if (!_config.GetValue<bool>("Stripe:Enabled"))
        {
            TempData["SubscriptionError"] = "Οι πληρωμές δεν είναι διαθέσιμες αυτή τη στιγμή.";
            return RedirectToAction(nameof(Index));
        }

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var userId = _userManager.GetUserId(User)!;
        var user = await _userManager.FindByIdAsync(userId);

        var domain = $"{Request.Scheme}://{Request.Host}";
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = _config["Stripe:PaidPriceId"],
                    Quantity = 1,
                }
            ],
            Mode = "subscription",
            SuccessUrl = $"{domain}/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{domain}/subscription/cancel",
            CustomerEmail = user?.Email,
            Metadata = new Dictionary<string, string> { ["userId"] = userId }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);
        return Redirect(session.Url);
    }

    [HttpGet("/subscription/success")]
    [Authorize]
    public async Task<IActionResult> Success(string? session_id)
    {
        var userId = _userManager.GetUserId(User)!;

        if (!string.IsNullOrEmpty(session_id) && _config.GetValue<bool>("Stripe:Enabled"))
        {
            try
            {
                StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
                var session = await new SessionService().GetAsync(session_id);

                if (session?.Status == "complete")
                {
                    await SetPlanAsync(userId, SubscriptionPlan.Paid,
                        session.CustomerId, session.SubscriptionId);

                    await _appLog.LogAsync(AppLogLevel.Info, "Subscription",
                        $"Plan upgraded to Paid via success page. SessionId={session_id}",
                        userId: userId);
                }
                else
                {
                    await _appLog.LogAsync(AppLogLevel.Warning, "Subscription",
                        $"Success page: session status='{session?.Status}', payment_status='{session?.PaymentStatus}'. No update.",
                        userId: userId);
                }
            }
            catch (Exception ex)
            {
                await _appLog.LogAsync(AppLogLevel.Error, "Subscription",
                    $"Success page Stripe error: {ex.Message}",
                    details: ex.ToString(), userId: userId);
            }
        }

        return View();
    }

    [HttpGet("/subscription/cancel")]
    public IActionResult Cancel()
    {
        return View();
    }

    [HttpPost("/subscription/webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? "";
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;
                if (session?.Metadata?.TryGetValue("userId", out var userId) == true && userId != null)
                {
                    await SetPlanAsync(userId, SubscriptionPlan.Paid,
                        session.CustomerId, session.SubscriptionId);

                    var paidUser = await _userManager.FindByIdAsync(userId);
                    if (paidUser is not null)
                        _ = Task.Run(async () =>
                        {
                            try { await _userEmail.SendPaymentSucceededAsync(paidUser); }
                            catch { /* best-effort */ }
                        });
                }
            }
            else if (stripeEvent.Type == "customer.subscription.deleted")
            {
                var stripeSub = stripeEvent.Data.Object as Stripe.Subscription;
                if (stripeSub != null)
                {
                    var sub = await _db.Subscriptions.FirstOrDefaultAsync(
                        s => s.StripeSubscriptionId == stripeSub.Id);
                    if (sub != null)
                    {
                        sub.Plan = SubscriptionPlan.Free;
                        sub.Status = SubscriptionStatus.Cancelled;
                        sub.EndDate = DateTime.UtcNow;
                        await _db.SaveChangesAsync();

                        var cancelledUser = await _userManager.FindByIdAsync(sub.UserId);
                        if (cancelledUser is not null)
                            _ = Task.Run(async () =>
                            {
                                try { await _userEmail.SendSubscriptionCancelledAsync(cancelledUser); }
                                catch { /* best-effort */ }
                            });
                    }
                }
            }
            else if (stripeEvent.Type == "invoice.payment_failed")
            {
                var invoice = stripeEvent.Data.Object as Stripe.Invoice;
                if (invoice?.CustomerId is not null)
                {
                    var failedSub = await _db.Subscriptions.FirstOrDefaultAsync(
                        s => s.StripeCustomerId == invoice.CustomerId);
                    if (failedSub is not null)
                    {
                        var failedUser = await _userManager.FindByIdAsync(failedSub.UserId);
                        if (failedUser is not null)
                            _ = Task.Run(async () =>
                            {
                                try { await _userEmail.SendPaymentFailedAsync(failedUser); }
                                catch { /* best-effort */ }
                            });
                    }
                }
            }

            return Ok();
        }
        catch (StripeException)
        {
            return BadRequest();
        }
    }

    private async Task SetPlanAsync(string userId, SubscriptionPlan plan,
        string? stripeCustomerId, string? stripeSubscriptionId)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (sub == null)
        {
            sub = new Models.Entities.Subscription { UserId = userId, StartDate = DateTime.UtcNow };
            _db.Subscriptions.Add(sub);
        }
        sub.Plan = plan;
        sub.Status = SubscriptionStatus.Active;
        sub.StripeCustomerId = stripeCustomerId;
        sub.StripeSubscriptionId = stripeSubscriptionId;
        await _db.SaveChangesAsync();
    }
}
