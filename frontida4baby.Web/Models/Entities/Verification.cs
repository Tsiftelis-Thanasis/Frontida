using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.Entities;

public enum VerificationDocumentType
{
    AddressProof   = 1,   // e.g. utility bill
    CriminalRecord = 2,   // Πιστοποιητικό ποινικού μητρώου
}

public enum DocumentReviewStatus
{
    Pending  = 0,
    Approved = 1,
    Rejected = 2,
}

public enum IdentityCheckStatus
{
    NotStarted = 0,
    Pending    = 1,
    Verified   = 2,
    Failed     = 3,
}

/// <summary>Per-user verification state that is not covered by ASP.NET Identity (email/phone flags).</summary>
public class UserVerification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    // Identity (KYC via Stripe Identity)
    public IdentityCheckStatus IdentityStatus { get; set; } = IdentityCheckStatus.NotStarted;
    public DateTime? IdentityVerifiedAt { get; set; }
    [MaxLength(100)]
    public string? StripeIdentitySessionId { get; set; }

    // Phone OTP throttling
    public DateTime? PhoneCodeSentAt { get; set; }
    public int PhoneCodesSentToday { get; set; }
    public DateTime? PhoneCodesCountDate { get; set; }
    public int PhoneCodeFailedAttempts { get; set; }

    // Approved document summary (kept even after the file itself is deleted)
    public DateTime? AddressApprovedAt { get; set; }
    public DateTime? AddressExpiresAt { get; set; }
    public DateTime? CriminalRecordIssuedOn { get; set; }
    public DateTime? CriminalRecordUploadedAt { get; set; }
    public DateTime? CriminalRecordApprovedAt { get; set; }
    public DateTime? CriminalRecordExpiresAt { get; set; }

    // Expiry reminders already sent (so the background job doesn't repeat itself)
    public DateTime? CriminalRecordWarningSentAt { get; set; }
    public DateTime? CriminalRecordExpiredNoticeSentAt { get; set; }
    public DateTime? AddressWarningSentAt { get; set; }
    public DateTime? AddressExpiredNoticeSentAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An uploaded verification document. Stored privately in the database (never under wwwroot).</summary>
public class VerificationDocument
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public VerificationDocumentType Type { get; set; }
    public DocumentReviewStatus Status { get; set; } = DocumentReviewStatus.Pending;

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    /// <summary>File bytes. Null once the file has been purged after review (see Verification:RetainDocuments).</summary>
    public byte[]? Data { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Date printed on the document, entered by the user and confirmed by the reviewer.</summary>
    public DateTime? IssuedOn { get; set; }

    public DateTime? ReviewedAt { get; set; }
    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }
    [MaxLength(500)]
    public string? RejectionReason { get; set; }
    public DateTime? DataPurgedAt { get; set; }
}
