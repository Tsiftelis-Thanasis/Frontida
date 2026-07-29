using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace frontida4baby.Web.Services;

/// <summary>
/// myDATA data-readiness only: persists a local invoice record with sequential
/// numbering and seller/buyer details. Does NOT transmit anything to AADE —
/// that requires the owner's registered company's real API credentials, which
/// don't exist yet. Swap in a real MyData-calling IInvoicingService later.
/// </summary>
public class LocalInvoicingService : IInvoicingService
{
    private readonly ApplicationDbContext _db;
    private readonly CompanyOptions _company;

    public LocalInvoicingService(ApplicationDbContext db, IOptions<CompanyOptions> company)
    {
        _db = db;
        _company = company.Value;
    }

    public async Task<Invoice> IssueInvoiceAsync(
        ApplicationUser buyer,
        string stripeInvoiceId,
        decimal totalAmount,
        string currency,
        DateTime issuedAt)
    {
        var existing = await _db.Invoices.FirstOrDefaultAsync(i => i.StripeInvoiceId == stripeInvoiceId);
        if (existing is not null)
            return existing;

        var year = issuedAt.Year;
        var prefix = $"{year}-";
        var lastNumber = await _db.Invoices
            .Where(i => i.Number.StartsWith(prefix))
            .OrderByDescending(i => i.Number)
            .Select(i => i.Number)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (lastNumber is not null && int.TryParse(lastNumber.AsSpan(prefix.Length), out var parsed))
            nextSeq = parsed + 1;

        var invoice = new Invoice
        {
            Number          = $"{prefix}{nextSeq:D5}",
            StripeInvoiceId = stripeInvoiceId,
            UserId          = buyer.Id,
            IssuedAt        = issuedAt,
            // No VAT breakdown yet — Stripe Automatic Tax is off by default until
            // the owner's Stripe Tax settings are configured (Stripe:AutomaticTaxEnabled).
            NetAmount       = totalAmount,
            VatAmount       = 0m,
            TotalAmount     = totalAmount,
            Currency        = currency,
            BuyerName       = $"{buyer.FirstName} {buyer.LastName}".Trim(),
            BuyerEmail      = buyer.Email ?? "",
            SellerName      = _company.Name,
            SellerVatNumber = _company.VatNumber,
            Status          = InvoiceStatus.Issued,
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }
}
