using frontida4baby.Web.Models.Entities;
using Stripe.Identity;

namespace frontida4baby.Web.Services;

public class VerificationSummary
{
    public string UserId { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }
    public string? PhoneNumber { get; set; }

    public bool AddressApproved { get; set; }
    public bool AddressPending { get; set; }
    public bool AddressExpired { get; set; }
    public DateTime? AddressExpiresAt { get; set; }
    public string? AddressRejectionReason { get; set; }

    public IdentityCheckStatus IdentityStatus { get; set; }

    public bool CriminalRecordApproved { get; set; }
    public bool CriminalRecordPending { get; set; }
    public bool CriminalRecordExpired { get; set; }
    public DateTime? CriminalRecordUploadedAt { get; set; }
    public DateTime? CriminalRecordIssuedOn { get; set; }
    public DateTime? CriminalRecordExpiresAt { get; set; }
    public string? CriminalRecordRejectionReason { get; set; }

    public bool IdentityVerified => IdentityStatus == IdentityCheckStatus.Verified;

    public bool IsFullyVerified =>
        EmailConfirmed && PhoneConfirmed && AddressApproved && IdentityVerified && CriminalRecordApproved;

    public int CompletedSteps =>
        (EmailConfirmed ? 1 : 0) + (PhoneConfirmed ? 1 : 0) + (AddressApproved ? 1 : 0)
        + (IdentityVerified ? 1 : 0) + (CriminalRecordApproved ? 1 : 0);

    public const int TotalSteps = 5;

    /// <summary>Greek labels of the steps that are still missing, for notices.</summary>
    public List<string> Missing()
    {
        var m = new List<string>();
        if (!EmailConfirmed) m.Add("Επιβεβαίωση email");
        if (!PhoneConfirmed) m.Add("Επιβεβαίωση κινητού");
        if (!AddressApproved) m.Add(AddressExpired ? "Απόδειξη διεύθυνσης (έληξε)" : "Απόδειξη διεύθυνσης");
        if (!IdentityVerified) m.Add("Έλεγχος ταυτότητας");
        if (!CriminalRecordApproved) m.Add(CriminalRecordExpired ? "Πιστοποιητικό ποινικού μητρώου (έληξε)" : "Πιστοποιητικό ποινικού μητρώου");
        return m;
    }
}

public interface IVerificationService
{
    Task<VerificationSummary> GetSummaryAsync(string userId);
    Task<Dictionary<string, VerificationSummary>> GetSummariesAsync(IEnumerable<string> userIds);

    /// <summary>Recompute Profile.IsVerified / VerifiedAt from the current state.</summary>
    Task SyncProfileAsync(string userId);

    Task<(bool Ok, string? Error)> SubmitDocumentAsync(
        string userId, VerificationDocumentType type, IFormFile file, DateTime? issuedOn);

    Task<(bool Ok, string? Error)> ReviewDocumentAsync(
        int documentId, string reviewerId, bool approve, DateTime? issuedOn, string? rejectionReason);

    Task<(bool Ok, string? Error)> SendPhoneCodeAsync(ApplicationUser user, string phone);
    Task<(bool Ok, string? Error)> VerifyPhoneCodeAsync(ApplicationUser user, string phone, string code);

    Task<(string? Url, string? Error)> StartIdentityCheckAsync(ApplicationUser user, string returnUrl);
    Task RefreshIdentityStatusAsync(string userId);
    Task ApplyIdentitySessionAsync(VerificationSession session);
}
