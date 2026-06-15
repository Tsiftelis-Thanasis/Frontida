using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace frontida4baby.Tests.Helpers;

/// <summary>
/// Extends TestWebApplicationFactory with Stripe test configuration.
/// Injects a known webhook secret so that StripeEventHelper-signed
/// payloads pass EventUtility.ConstructEvent validation.
/// </summary>
public class StripeWebhookTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder); // sets up in-memory DB + NoOpEmailService

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:WebhookSecret"] = StripeEventHelper.TestWebhookSecret,
                ["Stripe:Enabled"]       = "true",
                // SecretKey is not needed for webhook tests (no outbound Stripe calls)
                ["Stripe:SecretKey"]     = "sk_test_placeholder",
                ["Stripe:PaidPriceId"]   = "price_test_placeholder",
            });
        });
    }
}
