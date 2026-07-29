using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
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
    private readonly IInvoicingService _invoicing;

    public SubscriptionController(
        ISubscriptionService subscription,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserEmailService userEmail,
        IAppLogService appLog,
        IConfiguration config,
        IInvoicingService invoicing)
    {
        _subscription = subscription;
        _db = db;
        _userManager = userManager;
        _userEmail = userEmail;
        _appLog = appLog;
        _config = config;
        _invoicing = invoicing;
    }

    [HttpGet("/subscription")]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        SubscriptionPlan plan = SubscriptionPlan.Free;
        if (userId != null) plan = await _subscription.GetPlanAsync(userId);
        ViewBag.CurrentPlan   = plan;
        ViewBag.StripeEnabled = _config.GetValue<bool>("Stripe:Enabled");

        var hasStripeCustomer = false;
        var refundEligible = false;
        var refundWindowDays = _config.GetValue("Stripe:RefundWindowDays", 14);

        if (userId != null && plan == SubscriptionPlan.Paid)
        {
            var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
            hasStripeCustomer = sub?.StripeCustomerId != null;

            if (hasStripeCustomer && _config.GetValue<bool>("Stripe:Enabled"))
            {
                try
                {
                    StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
                    var invoices = await new InvoiceService().ListAsync(new InvoiceListOptions
                    {
                        Customer = sub!.StripeCustomerId,
                        Status   = "paid",
                        Limit    = 1,
                    });
                    var latest = invoices.Data.FirstOrDefault();
                    if (latest is not null)
                    {
                        var chargedAt = latest.StatusTransitions?.PaidAt ?? latest.Created;
                        refundEligible = chargedAt >= DateTime.UtcNow.AddDays(-refundWindowDays)
                            && latest.Id != sub.LastRefundedInvoiceId;
                    }
                }
                catch (StripeException) { /* leave refundEligible = false */ }
            }
        }

        ViewBag.HasStripeCustomer = hasStripeCustomer;
        ViewBag.RefundEligible    = refundEligible;
        ViewBag.RefundWindowDays  = refundWindowDays;
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

        // Requires Stripe Tax to be configured in the Stripe Dashboard first —
        // stays off until Stripe:AutomaticTaxEnabled is flipped on post company registration.
        if (_config.GetValue<bool>("Stripe:AutomaticTaxEnabled"))
            options.AutomaticTax = new SessionAutomaticTaxOptions { Enabled = true };

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

                if (session?.Status == "complete" && session.PaymentStatus == "paid"
                    && session.Metadata?.TryGetValue("userId", out var sessionUserId) == true
                    && sessionUserId == userId)
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

    // ── GET /subscription/portal — Stripe-hosted self-service (payment method, plain cancel) ──
    [HttpGet("/subscription/portal")]
    [Authorize]
    public async Task<IActionResult> Portal()
    {
        var userId = _userManager.GetUserId(User)!;
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (sub?.StripeCustomerId is null || !_config.GetValue<bool>("Stripe:Enabled"))
        {
            TempData["SubscriptionError"] = "Δεν βρέθηκε ενεργή συνδρομή.";
            return RedirectToAction(nameof(Index));
        }

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var domain = $"{Request.Scheme}://{Request.Host}";
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer  = sub.StripeCustomerId,
            ReturnUrl = $"{domain}/subscription",
        };
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(options);
        return Redirect(session.Url);
    }

    // ── GET /subscription/billing — customer's own payment history ───────────────
    [HttpGet("/subscription/billing")]
    [Authorize]
    public async Task<IActionResult> Billing()
    {
        var userId = _userManager.GetUserId(User)!;
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);

        if (sub?.StripeCustomerId is null || !_config.GetValue<bool>("Stripe:Enabled"))
            return View(new List<PaymentViewModel>());

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var invoiceService = new InvoiceService();
        var invoices = await invoiceService.ListAsync(new InvoiceListOptions
        {
            Customer = sub.StripeCustomerId,
            Limit = 100,
        });

        var payments = invoices.Data
            .Where(i => i.Status == "paid")
            .Select(i => new PaymentViewModel
            {
                InvoiceId        = i.Id,
                StripeCustomerId = i.CustomerId,
                UserEmail        = i.CustomerEmail ?? "",
                UserName         = "",
                Amount           = i.AmountPaid / 100m,
                Currency         = i.Currency.ToUpperInvariant(),
                Status           = i.Status,
                PaidAt           = i.StatusTransitions?.PaidAt ?? i.Created,
                InvoicePdfUrl    = i.InvoicePdf,
                HostedInvoiceUrl = i.HostedInvoiceUrl,
            })
            .OrderByDescending(p => p.PaidAt)
            .ToList();

        return View(payments);
    }

    // ── POST /subscription/refund — self-service, gated to Stripe:RefundWindowDays ──
    // One refund per billing period: rejected if the latest invoice was already refunded.
    [HttpPost("/subscription/refund")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund()
    {
        var userId = _userManager.GetUserId(User)!;
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);

        if (sub is null || sub.Plan != SubscriptionPlan.Paid || sub.StripeCustomerId is null
            || !_config.GetValue<bool>("Stripe:Enabled"))
        {
            TempData["SubscriptionError"] = "Δεν υπάρχει ενεργή συνδρομή Premium προς επιστροφή.";
            return RedirectToAction(nameof(Index));
        }

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var invoiceService = new InvoiceService();
        var invoices = await invoiceService.ListAsync(new InvoiceListOptions
        {
            Customer = sub.StripeCustomerId,
            Status   = "paid",
            Limit    = 1,
        });
        var latestInvoice = invoices.Data.FirstOrDefault();

        if (latestInvoice is null)
        {
            TempData["SubscriptionError"] = "Δεν βρέθηκε πληρωμή προς επιστροφή.";
            return RedirectToAction(nameof(Index));
        }

        var windowDays = _config.GetValue("Stripe:RefundWindowDays", 14);
        var chargedAt = latestInvoice.StatusTransitions?.PaidAt ?? latestInvoice.Created;

        if (chargedAt < DateTime.UtcNow.AddDays(-windowDays))
        {
            TempData["SubscriptionError"] =
                $"Η επιστροφή χρημάτων είναι διαθέσιμη μόνο εντός {windowDays} ημερών από τη χρέωση. " +
                "Επικοινώνησε μαζί μας για βοήθεια.";
            return RedirectToAction(nameof(Index));
        }

        if (latestInvoice.Id == sub.LastRefundedInvoiceId)
        {
            TempData["SubscriptionError"] = "Η χρέωση αυτής της περιόδου έχει ήδη επιστραφεί.";
            return RedirectToAction(nameof(Index));
        }

        var paymentIntentId = latestInvoice.Payments?.Data?.FirstOrDefault()?.Payment?.PaymentIntentId;
        if (string.IsNullOrEmpty(paymentIntentId))
        {
            TempData["SubscriptionError"] = "Δεν ήταν δυνατή η επιστροφή αυτής της χρέωσης. Επικοινώνησε μαζί μας.";
            return RedirectToAction(nameof(Index));
        }

        var refundService = new RefundService();
        var refund = await refundService.CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId, // full, non-prorated refund of the period's charge
        });

        if (!string.IsNullOrEmpty(sub.StripeSubscriptionId))
        {
            try { await new Stripe.SubscriptionService().CancelAsync(sub.StripeSubscriptionId); }
            catch (StripeException) { /* may already be cancelled */ }
        }

        sub.Plan = SubscriptionPlan.Free;
        sub.Status = SubscriptionStatus.Cancelled;
        sub.EndDate = DateTime.UtcNow;
        sub.LastRefundedInvoiceId = latestInvoice.Id;
        sub.RefundedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _appLog.LogAsync(AppLogLevel.Info, "Subscription",
            $"Self-service refund issued. InvoiceId={latestInvoice.Id} RefundId={refund.Id}",
            userId: userId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null)
            _ = Task.Run(async () =>
            {
                try { await _userEmail.SendRefundIssuedAsync(user, latestInvoice.AmountPaid / 100m, latestInvoice.Currency); }
                catch { /* best-effort */ }
            });

        TempData["SubscriptionMessage"] = "Η επιστροφή χρημάτων ολοκληρώθηκε. Η συνδρομή ακυρώθηκε.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/subscription/webhook")]
    [IgnoreAntiforgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? "";
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
            return BadRequest();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                webhookSecret,
                throwOnApiVersionMismatch: false);

            // Idempotency: skip already-processed events
            if (await _db.ProcessedStripeEvents.AnyAsync(e => e.EventId == stripeEvent.Id))
                return Ok();

            _db.ProcessedStripeEvents.Add(new Models.Entities.ProcessedStripeEvent
            {
                EventId = stripeEvent.Id,
            });
            await _db.SaveChangesAsync();

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
                        failedSub.Status = SubscriptionStatus.PastDue;
                        await _db.SaveChangesAsync();

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
            else if (stripeEvent.Type == "invoice.payment_succeeded")
            {
                var invoice = stripeEvent.Data.Object as Stripe.Invoice;
                if (invoice?.CustomerId is not null)
                {
                    var paidSub = await _db.Subscriptions.FirstOrDefaultAsync(
                        s => s.StripeCustomerId == invoice.CustomerId);
                    if (paidSub is not null)
                    {
                        if (paidSub.Status == SubscriptionStatus.PastDue)
                        {
                            paidSub.Status = SubscriptionStatus.Active;
                            await _db.SaveChangesAsync();
                        }

                        var invoicedUser = await _userManager.FindByIdAsync(paidSub.UserId);
                        if (invoicedUser is not null)
                        {
                            await _invoicing.IssueInvoiceAsync(
                                invoicedUser,
                                invoice.Id,
                                invoice.AmountPaid / 100m,
                                invoice.Currency,
                                invoice.StatusTransitions?.PaidAt ?? invoice.Created);
                        }
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
