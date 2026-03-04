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
    private readonly IConfiguration _config;

    public SubscriptionController(
        ISubscriptionService subscription,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        _subscription = subscription;
        _db = db;
        _userManager = userManager;
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
    public IActionResult Success()
    {
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
