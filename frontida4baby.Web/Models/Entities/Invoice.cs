using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.Entities;

public enum InvoiceStatus { Issued, Cancelled }

/// <summary>
/// Local durable record of a paid Stripe invoice, kept independently of Stripe so
/// the platform has its own sequential invoice numbering and seller/buyer details
/// ready for Greek myDATA (AADE) e-invoicing once the company registration and
/// API credentials exist. <see cref="MyDataMark"/> stays null until that real
/// integration is wired up — this is data-readiness only, not a live submission.
/// </summary>
public class Invoice
{
    public int Id { get; set; }

    /// <summary>Sequential per-year invoice number, e.g. "2026-00001".</summary>
    [Required, StringLength(20)]
    public string Number { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string StripeInvoiceId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    [StringLength(10)]
    public string Currency { get; set; } = "eur";

    [StringLength(200)]
    public string BuyerName { get; set; } = string.Empty;
    [StringLength(255)]
    public string BuyerEmail { get; set; } = string.Empty;
    [StringLength(20)]
    public string? BuyerVatNumber { get; set; }

    [StringLength(200)]
    public string SellerName { get; set; } = string.Empty;
    [StringLength(20)]
    public string SellerVatNumber { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    /// <summary>AADE myDATA transmission mark (MARK), populated once real myDATA submission exists.</summary>
    [StringLength(50)]
    public string? MyDataMark { get; set; }
}
