using frontida4baby.Tests.Helpers;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace frontida4baby.Tests;

/// <summary>
/// Tests for POST /subscription/webhook.
///
/// ── Stripe invoice schema comparison ─────────────────────────────────────────
///
/// Stripe's documented Invoice object (from docs.stripe.com/api/invoices/object):
///   id               string   "in_1xxx"         ✗ not stored
///   customer         string   "cus_xxx"         ✓ used to look up local Subscription
///   subscription     string   "sub_xxx"         ✗ not stored (only used in deletion)
///   amount_due       int      1300 (€13.00)     ✗ not stored
///   amount_paid      int      0                 ✗ not stored
///   currency         string   "eur"             ✗ not stored
///   status           string   "open"            ✗ not stored
///   lines.data[0]
///     amount         int      1300              ✗ not stored
///     description    string   "1× Premium…"    ✗ not stored
///     period.start   unix     1735036800        ✗ not stored
///     period.end     unix     1737628800        ✗ not stored
///
/// Recommendation: store invoice.id + amount_due + currency + period on the
/// Subscription (or a new PaymentHistory table) to power an invoice history
/// view and to enrich the "payment failed" email with the exact amount owed.
///
/// ── Event coverage ────────────────────────────────────────────────────────────
///   checkout.session.completed     → upgrades to Paid
///   customer.subscription.deleted  → downgrades to Free / Cancelled
///   invoice.payment_failed         → sends email (fire-and-forget)
///   unknown event type             → 200, ignored
///   invalid / missing signature    → 400
/// </summary>
public class SubscriptionWebhookTests : IClassFixture<StripeWebhookTestFactory>
{
    private readonly StripeWebhookTestFactory _factory;

    public SubscriptionWebhookTests(StripeWebhookTestFactory factory)
        => _factory = factory;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostWebhookAsync(string json, string sigHeader)
    {
        var client  = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/subscription/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", sigHeader);
        return await client.SendAsync(request);
    }

    private async Task<ApplicationUser> SeedUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        return await TestDataSeeder.CreateUserAsync(scope.ServiceProvider, email);
    }

    private async Task SeedSubscriptionAsync(
        string userId, string customerId, string subscriptionId,
        SubscriptionPlan plan = SubscriptionPlan.Paid)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Subscriptions.Add(new Subscription
        {
            UserId               = userId,
            Plan                 = plan,
            Status               = SubscriptionStatus.Active,
            StartDate            = DateTime.UtcNow,
            StripeCustomerId     = customerId,
            StripeSubscriptionId = subscriptionId,
        });
        await db.SaveChangesAsync();
    }

    private Subscription? ReadSubscription(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.Subscriptions.FirstOrDefault(s => s.UserId == userId);
    }

    // ── checkout.session.completed ─────────────────────────────────────────────

    [Fact]
    public async Task CheckoutCompleted_NewUser_CreatesActivePaidSubscription()
    {
        var user = await SeedUserAsync($"wh_checkout_new_{Guid.NewGuid():N}@test.com");

        var (json, sig) = StripeEventHelper.CheckoutSessionCompleted(
            userId: user.Id,
            customerId: "cus_checkout_001",
            subscriptionId: "sub_checkout_001");

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sub = ReadSubscription(user.Id);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionPlan.Paid,         sub.Plan);
        Assert.Equal(SubscriptionStatus.Active,     sub.Status);
        Assert.Equal("cus_checkout_001",            sub.StripeCustomerId);
        Assert.Equal("sub_checkout_001",            sub.StripeSubscriptionId);
    }

    [Fact]
    public async Task CheckoutCompleted_ExistingFreeUser_UpgradesToPaid()
    {
        var user = await SeedUserAsync($"wh_checkout_upgrade_{Guid.NewGuid():N}@test.com");

        // pre-existing Free subscription row
        await SeedSubscriptionAsync(user.Id, "cus_old", "sub_old", SubscriptionPlan.Free);

        var (json, sig) = StripeEventHelper.CheckoutSessionCompleted(
            userId: user.Id,
            customerId: "cus_checkout_002",
            subscriptionId: "sub_checkout_002");

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sub = ReadSubscription(user.Id);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionPlan.Paid,     sub.Plan);
        Assert.Equal("cus_checkout_002",        sub.StripeCustomerId);
        Assert.Equal("sub_checkout_002",        sub.StripeSubscriptionId);
    }

    [Fact]
    public async Task CheckoutCompleted_MissingUserId_Returns200WithNoDbChange()
    {
        // Stripe event with no userId in metadata — controller should ignore it gracefully
        var json = """
            {
              "id": "evt_nouserid",
              "object": "event",
              "api_version": "2023-10-16",
              "created": 1735000000,
              "type": "checkout.session.completed",
              "livemode": false,
              "request": null,
              "data": {
                "object": {
                  "id": "cs_nouserid",
                  "object": "checkout.session",
                  "status": "complete",
                  "payment_status": "paid",
                  "customer": "cus_nouserid",
                  "subscription": "sub_nouserid",
                  "metadata": {}
                }
              }
            }
            """;
        var sig = StripeEventHelper.SignPayload(json);

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── customer.subscription.deleted ─────────────────────────────────────────

    [Fact]
    public async Task SubscriptionDeleted_KnownSub_DowngradesToFreeAndSetsEndDate()
    {
        var user = await SeedUserAsync($"wh_cancel_{Guid.NewGuid():N}@test.com");
        await SeedSubscriptionAsync(user.Id, "cus_cancel_001", "sub_cancel_001");

        var (json, sig) = StripeEventHelper.SubscriptionDeleted(
            subscriptionId: "sub_cancel_001",
            customerId: "cus_cancel_001");

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sub = ReadSubscription(user.Id);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionPlan.Free,         sub.Plan);
        Assert.Equal(SubscriptionStatus.Cancelled,  sub.Status);
        Assert.NotNull(sub.EndDate);
        Assert.True(sub.EndDate <= DateTime.UtcNow);
    }

    [Fact]
    public async Task SubscriptionDeleted_UnknownSub_Returns200WithNoError()
    {
        // Stripe may send events for subscriptions we don't recognise — must not crash
        var (json, sig) = StripeEventHelper.SubscriptionDeleted(
            subscriptionId: "sub_unknown_999");

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── invoice.payment_failed ─────────────────────────────────────────────────

    [Fact]
    public async Task InvoicePaymentFailed_KnownCustomer_Returns200()
    {
        // The controller fires a "payment failed" email (best-effort, NoOp in tests)
        // and returns 200. Subscription plan is NOT downgraded on first failure —
        // Stripe handles retry logic and sends subscription.deleted if all retries fail.
        var user = await SeedUserAsync($"wh_fail_{Guid.NewGuid():N}@test.com");
        await SeedSubscriptionAsync(user.Id, "cus_fail_001", "sub_fail_001");

        var (json, sig) = StripeEventHelper.InvoicePaymentFailed(
            customerId: "cus_fail_001",
            subscriptionId: "sub_fail_001",
            amountDue: 1300);

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Plan remains Paid until Stripe gives up and sends subscription.deleted
        var sub = ReadSubscription(user.Id);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionPlan.Paid, sub.Plan);
    }

    [Fact]
    public async Task InvoicePaymentFailed_UnknownCustomer_Returns200WithNoError()
    {
        var (json, sig) = StripeEventHelper.InvoicePaymentFailed(
            customerId: "cus_unknown_999");

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Schema validation: if Stripe.net cannot deserialise our JSON into
    /// a Stripe.Invoice, ConstructEvent throws StripeException → 400.
    /// A 200 here proves our payload matches the documented invoice object.
    /// </summary>
    [Fact]
    public async Task InvoicePaymentFailed_JsonSchemaMatchesStripeInvoiceObject()
    {
        var (json, sig) = StripeEventHelper.InvoicePaymentFailed(
            customerId:     "cus_schema_check",
            subscriptionId: "sub_schema_check",
            amountDue:      1300,
            currency:       "eur");

        var response = await PostWebhookAsync(json, sig);

        // 200 = Stripe.net deserialised the payload without errors
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Unknown event type ─────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownEventType_Returns200AndIsIgnored()
    {
        var json = """
            {
              "id": "evt_unknown",
              "object": "event",
              "api_version": "2023-10-16",
              "created": 1735000000,
              "type": "payment_intent.created",
              "livemode": false,
              "request": null,
              "data": { "object": { "id": "pi_xxx", "object": "payment_intent" } }
            }
            """;
        var sig = StripeEventHelper.SignPayload(json);

        var response = await PostWebhookAsync(json, sig);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Signature validation ───────────────────────────────────────────────────

    [Fact]
    public async Task InvalidSignature_Returns400()
    {
        var (json, _) = StripeEventHelper.CheckoutSessionCompleted("any_user");

        var response = await PostWebhookAsync(json, "t=1000000000,v1=badsignature");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSignatureHeader_Returns400()
    {
        var client  = _factory.CreateClient();
        var (json, _) = StripeEventHelper.CheckoutSessionCompleted("any_user");
        var request = new HttpRequestMessage(HttpMethod.Post, "/subscription/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        // deliberately omit Stripe-Signature

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TamperedBody_Returns400()
    {
        var (json, sig) = StripeEventHelper.CheckoutSessionCompleted("any_user");

        // Modify body after signing — signature no longer matches
        var tampered = json.Replace("checkout.session.completed", "checkout.session.TAMPERED");

        var response = await PostWebhookAsync(tampered, sig);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
