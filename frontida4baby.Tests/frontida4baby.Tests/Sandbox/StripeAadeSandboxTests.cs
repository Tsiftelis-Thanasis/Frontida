using System.Net;
using System.Text;
using System.Text.Json;
using frontida4baby.Web.Data;
using EntityInvoice = frontida4baby.Web.Models.Entities.Invoice;
using AppUser = frontida4baby.Web.Models.Entities.ApplicationUser;
using File = System.IO.File;
using frontida4baby.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Xunit.Abstractions;

namespace frontida4baby.Tests.Sandbox;

// Run with: set STRIPE_TEST_SECRET_KEY=sk_test_... && dotnet test --filter Category=Sandbox
// Creates Stripe TEST-mode payments only. Nothing is sent to AADE; the myDATA
// payloads are written as dummy files for review.
[Trait("Category", "Sandbox")]
public class StripeAadeSandboxTests
{
    private const string KeyVariable = "STRIPE_TEST_SECRET_KEY";
    private readonly ITestOutputHelper _output;

    public StripeAadeSandboxTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task CreatesTestPaymentsIssuesInvoicesAndWritesDummyAadePayloads()
    {
        var stripeKey = Environment.GetEnvironmentVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(stripeKey) || !stripeKey.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            _output.WriteLine($"Skipped: set {KeyVariable} to a Stripe test secret key (sk_test_...) to run.");
            return;
        }

        var runDir = Path.Combine(FindRepoRoot(), "local-output", $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(runDir);

        var paymentIntents = new PaymentIntentService(new StripeClient(stripeKey));
        var payments = new List<PaymentIntent>();
        foreach (var amountCents in new long[] { 1990, 2990, 4990 })
        {
            payments.Add(await paymentIntents.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = amountCents,
                Currency = "eur",
                PaymentMethod = "pm_card_visa",
                PaymentMethodTypes = new List<string> { "card" },
                Confirm = true,
                Description = "Sandbox subscription payment",
                Metadata = new Dictionary<string, string> { ["source"] = "sandbox" },
            }));
        }

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);
        var company = Options.Create(new CompanyOptions
        {
            Name = "frontida4all (sandbox)",
            VatNumber = "000000000",
            TaxOffice = "Sandbox",
            Address = "Sandbox",
        });
        var invoicing = new LocalInvoicingService(db, company);
        var buyer = new AppUser
        {
            Id = "sandbox-buyer",
            Email = "sandbox.buyer@example.com",
            FirstName = "Sandbox",
            LastName = "Buyer",
        };

        var rows = new List<(PaymentIntent Payment, EntityInvoice Invoice, string DummyFile)>();
        var sequence = 0;
        foreach (var pi in payments)
        {
            sequence++;
            var invoice = await invoicing.IssueInvoiceAsync(
                buyer, pi.Id, pi.Amount / 100m, pi.Currency.ToUpperInvariant(), pi.Created);

            WriteJson(Path.Combine(runDir, "stripe", $"{pi.Id}.json"), new
            {
                pi.Id,
                pi.Amount,
                pi.Currency,
                pi.Status,
                pi.Created,
                pi.Description,
            });
            WriteJson(Path.Combine(runDir, "invoices", $"{invoice.Number}.json"), invoice);
            var dummyFile = Path.Combine("aade-dummy", $"{invoice.Number}.json");
            WriteJson(Path.Combine(runDir, dummyFile), BuildDummyAadePayload(invoice, sequence));

            rows.Add((pi, invoice, dummyFile.Replace('\\', '/')));
        }

        await File.WriteAllTextAsync(Path.Combine(runDir, "index.html"), BuildReport(rows));
        _output.WriteLine($"Sandbox output written to {runDir}");
        Assert.Equal(payments.Count, rows.Count);
    }

    private static object BuildDummyAadePayload(EntityInvoice invoice, int sequence) => new
    {
        Note = "DUMMY: nothing was sent to AADE. This shows the data a myDATA request would contain.",
        Environment = "sandbox",
        DummyMark = $"DUMMY-{sequence:D6}",
        InvoiceNumber = invoice.Number,
        IssueDate = invoice.IssuedAt,
        InvoiceType = "11.2",
        Seller = new { invoice.SellerName, invoice.SellerVatNumber },
        Buyer = new { invoice.BuyerName, invoice.BuyerEmail },
        NetValue = invoice.NetAmount,
        VatAmount = invoice.VatAmount,
        GrossValue = invoice.TotalAmount,
        invoice.Currency,
        IncomeClassification = new
        {
            ClassificationType = "E3_561_003",
            ClassificationCategory = "category1_3",
            Amount = invoice.TotalAmount,
        },
    };

    private static string BuildReport(IEnumerable<(PaymentIntent Payment, EntityInvoice Invoice, string DummyFile)> rows)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Sandbox report</title>")
            .Append("<style>body{font-family:system-ui,sans-serif;margin:2rem}")
            .Append("table{border-collapse:collapse}td,th{border:1px solid #ccc;padding:.4rem .7rem;text-align:left}</style>")
            .Append("</head><body><h1>Sandbox report</h1>")
            .Append($"<p>Generated {DateTime.UtcNow:u}. Stripe test mode. Nothing was sent to AADE.</p>")
            .Append("<table><tr><th>Stripe payment</th><th>Amount</th><th>Status</th><th>Created (UTC)</th>")
            .Append("<th>Invoice</th><th>AADE dummy payload</th></tr>");

        foreach (var (payment, invoice, dummyFile) in rows)
        {
            html.Append("<tr>")
                .Append($"<td><a href=\"stripe/{WebUtility.HtmlEncode(payment.Id)}.json\">{WebUtility.HtmlEncode(payment.Id)}</a></td>")
                .Append($"<td>{payment.Amount / 100m:0.00} {WebUtility.HtmlEncode(payment.Currency.ToUpperInvariant())}</td>")
                .Append($"<td>{WebUtility.HtmlEncode(payment.Status)}</td>")
                .Append($"<td>{payment.Created:u}</td>")
                .Append($"<td><a href=\"invoices/{WebUtility.HtmlEncode(invoice.Number)}.json\">{WebUtility.HtmlEncode(invoice.Number)}</a></td>")
                .Append($"<td><a href=\"{WebUtility.HtmlEncode(dummyFile)}\">{WebUtility.HtmlEncode(dummyFile)}</a></td>")
                .Append("</tr>");
        }

        html.Append("</table></body></html>");
        return html.ToString();
    }

    private static void WriteJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontida4baby.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
