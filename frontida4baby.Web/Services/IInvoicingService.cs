using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

/// <summary>
/// Issues a durable local invoice record for a successful Stripe payment.
/// Today this only persists the record (myDATA data-readiness). A future
/// MyDataInvoicingService can implement this same interface to also submit
/// to AADE's myDATA once the owner's company has real API credentials —
/// the webhook code that calls this won't need to change.
/// </summary>
public interface IInvoicingService
{
    Task<Invoice> IssueInvoiceAsync(
        ApplicationUser buyer,
        string stripeInvoiceId,
        decimal totalAmount,
        string currency,
        DateTime issuedAt);
}
