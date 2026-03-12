namespace frontida4baby.Web.Models.ViewModels;

public class PaymentViewModel
{
    public string InvoiceId { get; set; } = "";
    public string StripeCustomerId { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string UserName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime PaidAt { get; set; }
    public string? InvoicePdfUrl { get; set; }
    public string? HostedInvoiceUrl { get; set; }
    public string? ReceiptUrl { get; set; }
}
