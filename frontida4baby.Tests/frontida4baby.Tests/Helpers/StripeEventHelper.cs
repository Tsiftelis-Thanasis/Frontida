using System.Security.Cryptography;
using System.Text;

namespace frontida4baby.Tests.Helpers;

/// <summary>
/// Builds Stripe-signed webhook event payloads for testing.
///
/// Stripe's signature algorithm:
///   signed_payload = "{unix_timestamp}.{raw_body}"
///   signature      = HMAC-SHA256(key=secret_bytes, data=signed_payload)
///   header         = "t={unix_timestamp},v1={hex_signature}"
///
/// Stripe's documented invoice fields (for comparison):
///   id, customer, subscription, amount_due, amount_paid, currency, status,
///   lines.data[].amount, lines.data[].description, lines.data[].period,
///   hosted_invoice_url, invoice_pdf
///
/// What this project currently stores from invoices:
///   - Only invoice.customer (to look up the local Subscription row).
///   - Nothing else: no invoice.id, amount_due, currency, period.
/// </summary>
public static class StripeEventHelper
{
    /// <summary>Plain-text secret used across all webhook tests.</summary>
    public const string TestWebhookSecret = "test_webhook_secret_for_unit_tests_only";

    // ── Public event builders ──────────────────────────────────────────────────

    /// <summary>
    /// Stripe sends this when a Checkout Session finishes successfully.
    /// The controller reads session.Metadata["userId"], session.CustomerId,
    /// and session.SubscriptionId.
    /// </summary>
    public static (string json, string signatureHeader) CheckoutSessionCompleted(
        string userId,
        string customerId     = "cus_test_001",
        string subscriptionId = "sub_test_001")
    {
        var json = $$"""
            {
              "id": "evt_{{Guid.NewGuid():N}}",
              "object": "event",
              "api_version": "2023-10-16",
              "created": {{NowUnix()}},
              "type": "checkout.session.completed",
              "livemode": false,
              "request": null,
              "data": {
                "object": {
                  "id": "cs_{{Guid.NewGuid():N}}",
                  "object": "checkout.session",
                  "status": "complete",
                  "payment_status": "paid",
                  "customer": "{{customerId}}",
                  "subscription": "{{subscriptionId}}",
                  "metadata": { "userId": "{{userId}}" }
                }
              }
            }
            """;
        return (json, SignPayload(json));
    }

    /// <summary>
    /// Stripe sends this when a subscription is cancelled (immediately or at period end).
    /// The controller looks up the local Subscription row by StripeSubscriptionId.
    /// </summary>
    public static (string json, string signatureHeader) SubscriptionDeleted(
        string subscriptionId = "sub_test_001",
        string customerId     = "cus_test_001")
    {
        var json = $$"""
            {
              "id": "evt_{{Guid.NewGuid():N}}",
              "object": "event",
              "api_version": "2023-10-16",
              "created": {{NowUnix()}},
              "type": "customer.subscription.deleted",
              "livemode": false,
              "request": null,
              "data": {
                "object": {
                  "id": "{{subscriptionId}}",
                  "object": "subscription",
                  "customer": "{{customerId}}",
                  "status": "canceled",
                  "items": {
                    "object": "list",
                    "data": [],
                    "has_more": false,
                    "url": "/v1/subscription_items?subscription={{subscriptionId}}"
                  }
                }
              }
            }
            """;
        return (json, SignPayload(json));
    }

    /// <summary>
    /// Stripe sends this when a recurring payment fails (e.g. card declined).
    ///
    /// Stripe's documented Invoice object fields included here:
    ///   id            - unique invoice identifier (e.g. "in_xxx")
    ///   customer      - Stripe customer ID
    ///   subscription  - Stripe subscription ID
    ///   amount_due    - total owed in smallest currency unit (cents)
    ///   amount_paid   - 0 for a failed payment
    ///   currency      - ISO 4217 code ("eur")
    ///   status        - "open" while unpaid
    ///   lines.data    - one entry per subscription item
    ///     .amount           - line amount (matches amount_due for single-item subs)
    ///     .description      - human-readable plan description
    ///     .period.start/end - Unix timestamps for the billing period
    ///     .type             - "subscription"
    ///
    /// NOTE: the controller only reads invoice.CustomerId.
    ///       Fields like invoice.id, amount_due, and period are NOT persisted.
    ///       See SubscriptionWebhookTests for improvement notes.
    /// </summary>
    public static (string json, string signatureHeader) InvoicePaymentFailed(
        string customerId     = "cus_test_001",
        string subscriptionId = "sub_test_001",
        long   amountDue      = 1300,           // €13.00
        string currency       = "eur")
    {
        var now       = NowUnix();
        var periodEnd = now + 30L * 24 * 3600;  // ~1 month ahead
        var invoiceId = $"in_{Guid.NewGuid():N}";
        var lineId    = $"il_{Guid.NewGuid():N}";

        var json = $$"""
            {
              "id": "evt_{{Guid.NewGuid():N}}",
              "object": "event",
              "api_version": "2023-10-16",
              "created": {{now}},
              "type": "invoice.payment_failed",
              "livemode": false,
              "request": null,
              "data": {
                "object": {
                  "id": "{{invoiceId}}",
                  "object": "invoice",
                  "customer": "{{customerId}}",
                  "subscription": "{{subscriptionId}}",
                  "amount_due": {{amountDue}},
                  "amount_paid": 0,
                  "currency": "{{currency}}",
                  "status": "open",
                  "lines": {
                    "object": "list",
                    "data": [
                      {
                        "id": "{{lineId}}",
                        "object": "line_item",
                        "amount": {{amountDue}},
                        "currency": "{{currency}}",
                        "description": "1 \u00d7 frontida4all Premium (at \u20ac13.00 / month)",
                        "period": { "start": {{now}}, "end": {{periodEnd}} },
                        "type": "subscription"
                      }
                    ],
                    "has_more": false,
                    "url": "/v1/invoices/{{invoiceId}}/lines"
                  }
                }
              }
            }
            """;
        return (json, SignPayload(json));
    }

    /// <summary>
    /// Computes the Stripe-Signature header value for a given raw body.
    /// Exposed publicly so callers can sign arbitrary payloads.
    /// </summary>
    public static string SignPayload(string rawBody)
    {
        var ts          = NowUnix();
        var signedData  = $"{ts}.{rawBody}";
        var secretBytes = Encoding.UTF8.GetBytes(TestWebhookSecret);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedData));
        var sig  = Convert.ToHexString(hash).ToLower();

        return $"t={ts},v1={sig}";
    }

    private static long NowUnix() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
