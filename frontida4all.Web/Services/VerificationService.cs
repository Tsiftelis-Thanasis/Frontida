using System.Text.RegularExpressions;
using frontida4all.Web.Data;
using frontida4all.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Identity;

namespace frontida4all.Web.Services;

public class VerificationService : IVerificationService
{
    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    private const int MaxPhoneCodesPerDay = 5;
    private const int MaxPhoneFailedAttempts = 5;
    private static readonly TimeSpan PhoneCodeCooldown = TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ISmsSender _sms;
    private readonly IUserEmailService _email;
    private readonly IConfiguration _cfg;
    private readonly VerificationOptions _opts;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        ISmsSender sms,
        IUserEmailService email,
        IConfiguration cfg,
        IOptions<VerificationOptions> opts,
        ILogger<VerificationService> logger)
    {
        _db = db; _users = users; _sms = sms; _email = email; _cfg = cfg; _opts = opts.Value; _logger = logger;
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    public async Task<VerificationSummary> GetSummaryAsync(string userId)
    {
        var all = await GetSummariesAsync([userId]);
        return all[userId];
    }

    public async Task<Dictionary<string, VerificationSummary>> GetSummariesAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var now = DateTime.UtcNow;

        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.EmailConfirmed, u.PhoneNumber, u.PhoneNumberConfirmed })
            .ToListAsync();
        var vers = await _db.UserVerifications.AsNoTracking()
            .Where(v => ids.Contains(v.UserId)).ToListAsync();
        var docs = await _db.VerificationDocuments.AsNoTracking()
            .Where(d => ids.Contains(d.UserId))
            .Select(d => new { d.UserId, d.Type, d.Status, d.UploadedAt, d.RejectionReason })
            .ToListAsync();

        var result = new Dictionary<string, VerificationSummary>();
        foreach (var id in ids)
        {
            var u = users.FirstOrDefault(x => x.Id == id);
            var v = vers.FirstOrDefault(x => x.UserId == id);
            var s = new VerificationSummary
            {
                UserId = id,
                EmailConfirmed = u?.EmailConfirmed ?? false,
                PhoneConfirmed = (u?.PhoneNumberConfirmed ?? false) && !string.IsNullOrEmpty(u?.PhoneNumber),
                PhoneNumber = u?.PhoneNumber,
                IdentityStatus = v?.IdentityStatus ?? IdentityCheckStatus.NotStarted,
            };

            if (v?.AddressApprovedAt != null && v.AddressExpiresAt != null)
            {
                s.AddressExpiresAt = v.AddressExpiresAt;
                s.AddressApproved = v.AddressExpiresAt > now;
                s.AddressExpired = !s.AddressApproved;
            }
            if (v?.CriminalRecordApprovedAt != null && v.CriminalRecordExpiresAt != null)
            {
                s.CriminalRecordExpiresAt = v.CriminalRecordExpiresAt;
                s.CriminalRecordIssuedOn = v.CriminalRecordIssuedOn;
                s.CriminalRecordApproved = v.CriminalRecordExpiresAt > now;
                s.CriminalRecordExpired = !s.CriminalRecordApproved;
            }
            s.CriminalRecordUploadedAt = v?.CriminalRecordUploadedAt;

            var mine = docs.Where(d => d.UserId == id).ToList();
            var address = mine.Where(d => d.Type == VerificationDocumentType.AddressProof).ToList();
            var criminal = mine.Where(d => d.Type == VerificationDocumentType.CriminalRecord).ToList();
            s.AddressPending = address.Any(d => d.Status == DocumentReviewStatus.Pending);
            s.CriminalRecordPending = criminal.Any(d => d.Status == DocumentReviewStatus.Pending);
            s.AddressRejectionReason = LastRejection(address.Select(d => (d.Status, d.UploadedAt, d.RejectionReason)));
            s.CriminalRecordRejectionReason = LastRejection(criminal.Select(d => (d.Status, d.UploadedAt, d.RejectionReason)));

            result[id] = s;
        }
        return result;
    }

    /// <summary>Reason of the newest document if that newest document was rejected (so the user knows to re-upload).</summary>
    private static string? LastRejection(IEnumerable<(DocumentReviewStatus Status, DateTime UploadedAt, string? Reason)> docs)
    {
        var last = docs.OrderByDescending(d => d.UploadedAt).Cast<(DocumentReviewStatus Status, DateTime UploadedAt, string? Reason)?>().FirstOrDefault();
        if (last is null) return null;
        return last.Value.Status == DocumentReviewStatus.Rejected ? (last.Value.Reason ?? "Απορρίφθηκε") : null;
    }

    public async Task SyncProfileAsync(string userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (user is null || profile is null) return;

        var summary = await GetSummaryAsync(userId);
        var verified = user.IsCaregiver && summary.IsFullyVerified;
        if (profile.IsVerified == verified) return;

        profile.IsVerified = verified;
        profile.VerifiedAt = verified ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
    }

    private async Task<UserVerification> GetOrCreateAsync(string userId)
    {
        var v = await _db.UserVerifications.FirstOrDefaultAsync(x => x.UserId == userId);
        if (v is null)
        {
            v = new UserVerification { UserId = userId };
            _db.UserVerifications.Add(v);
        }
        return v;
    }

    // ── Documents ────────────────────────────────────────────────────────────

    public async Task<(bool Ok, string? Error)> SubmitDocumentAsync(
        string userId, VerificationDocumentType type, IFormFile file, DateTime? issuedOn)
    {
        if (file is null || file.Length == 0) return (false, "Επιλέξτε αρχείο.");
        if (file.Length > _opts.MaxFileSizeMb * 1024L * 1024L)
            return (false, $"Το αρχείο ξεπερνά τα {_opts.MaxFileSizeMb} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (false, "Επιτρέπονται μόνο αρχεία PDF, JPG ή PNG.");

        if (type == VerificationDocumentType.CriminalRecord)
        {
            if (issuedOn is null) return (false, "Συμπληρώστε την ημερομηνία έκδοσης του πιστοποιητικού.");
            if (issuedOn.Value.Date > DateTime.UtcNow.Date) return (false, "Η ημερομηνία έκδοσης δεν μπορεί να είναι στο μέλλον.");
            if (issuedOn.Value.Date.AddMonths(_opts.CriminalRecordValidityMonths) <= DateTime.UtcNow)
                return (false, $"Το πιστοποιητικό πρέπει να έχει εκδοθεί τους τελευταίους {_opts.CriminalRecordValidityMonths} μήνες.");
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        if (!LooksLike(bytes, ext)) return (false, "Το περιεχόμενο του αρχείου δεν ταιριάζει με τον τύπο του.");

        // A new upload replaces any earlier pending one of the same type.
        var oldPending = await _db.VerificationDocuments
            .Where(d => d.UserId == userId && d.Type == type && d.Status == DocumentReviewStatus.Pending)
            .ToListAsync();
        _db.VerificationDocuments.RemoveRange(oldPending);

        var safeName = Path.GetFileName(file.FileName);
        _db.VerificationDocuments.Add(new VerificationDocument
        {
            UserId = userId,
            Type = type,
            FileName = safeName.Length > 200 ? "document" + ext : safeName,
            ContentType = ext switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" },
            FileSize = bytes.Length,
            Data = bytes,
            IssuedOn = type == VerificationDocumentType.CriminalRecord ? AsUtcDate(issuedOn!.Value) : null,
        });

        if (type == VerificationDocumentType.CriminalRecord)
        {
            var v = await GetOrCreateAsync(userId);
            v.CriminalRecordUploadedAt = DateTime.UtcNow;
            v.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Form-bound dates have Kind=Unspecified, which Npgsql refuses for timestamptz columns.</summary>
    private static DateTime AsUtcDate(DateTime d) => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);

    private static bool LooksLike(byte[] b, string ext)
    {
        if (b.Length < 8) return false;
        return ext switch
        {
            ".pdf" => b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46,            // %PDF
            ".png" => b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
            _      => b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        };
    }

    public async Task<(bool Ok, string? Error)> ReviewDocumentAsync(
        int documentId, string reviewerId, bool approve, DateTime? issuedOn, string? rejectionReason)
    {
        var doc = await _db.VerificationDocuments.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return (false, "Το έγγραφο δεν βρέθηκε.");
        if (doc.Status != DocumentReviewStatus.Pending) return (false, "Το έγγραφο έχει ήδη εξεταστεί.");

        var now = DateTime.UtcNow;
        var v = await GetOrCreateAsync(doc.UserId);

        if (approve)
        {
            if (doc.Type == VerificationDocumentType.CriminalRecord)
            {
                var issued = (issuedOn ?? doc.IssuedOn) is { } rawIssued ? AsUtcDate(rawIssued) : (DateTime?)null;
                if (issued is null) return (false, "Απαιτείται ημερομηνία έκδοσης.");
                if (issued > now.Date) return (false, "Η ημερομηνία έκδοσης είναι στο μέλλον.");
                var expires = issued.Value.AddMonths(_opts.CriminalRecordValidityMonths);
                if (expires <= now) return (false, "Το πιστοποιητικό έχει ήδη λήξει — απορρίψτε το.");

                doc.IssuedOn = issued;
                v.CriminalRecordIssuedOn = issued;
                v.CriminalRecordApprovedAt = now;
                v.CriminalRecordExpiresAt = expires;
                v.CriminalRecordWarningSentAt = null;
                v.CriminalRecordExpiredNoticeSentAt = null;
            }
            else
            {
                v.AddressApprovedAt = now;
                v.AddressExpiresAt = now.AddMonths(_opts.AddressProofValidityMonths);
                v.AddressWarningSentAt = null;
                v.AddressExpiredNoticeSentAt = null;
            }
            doc.Status = DocumentReviewStatus.Approved;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rejectionReason)) return (false, "Γράψτε τον λόγο απόρριψης.");
            var reason = rejectionReason.Trim();
            doc.Status = DocumentReviewStatus.Rejected;
            doc.RejectionReason = reason.Length > 500 ? reason[..500] : reason;
        }

        doc.ReviewedAt = now;
        doc.ReviewedByUserId = reviewerId;
        if (!_opts.RetainDocuments)
        {
            doc.Data = null;
            doc.DataPurgedAt = now;
        }
        v.UpdatedAt = now;
        await _db.SaveChangesAsync();
        await SyncProfileAsync(doc.UserId);

        try { await _email.SendVerificationReviewAsync(doc.User, doc.Type, approve, doc.RejectionReason); }
        catch (Exception ex) { _logger.LogWarning(ex, "Verification review email failed for {UserId}", doc.UserId); }

        return (true, null);
    }

    // ── Phone ────────────────────────────────────────────────────────────────

    public static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var p = Regex.Replace(raw, @"[\s\-\(\)\.]", "");
        if (p.StartsWith("00")) p = "+" + p[2..];
        if (Regex.IsMatch(p, @"^(69\d{8}|2\d{9})$")) p = "+30" + p;   // Greek number without prefix
        return Regex.IsMatch(p, @"^\+[1-9]\d{7,14}$") ? p : null;
    }

    public async Task<(bool Ok, string? Error)> SendPhoneCodeAsync(ApplicationUser user, string phone)
    {
        var number = NormalizePhone(phone);
        if (number is null) return (false, "Μη έγκυρος αριθμός. Χρησιμοποιήστε μορφή 69XXXXXXXX ή +30…");
        if (!_sms.IsAvailable) return (false, "Η αποστολή SMS δεν είναι διαθέσιμη αυτή τη στιγμή.");

        if (await _db.Users.AnyAsync(u => u.Id != user.Id && u.PhoneNumber == number && u.PhoneNumberConfirmed))
            return (false, "Αυτός ο αριθμός χρησιμοποιείται ήδη σε άλλο λογαριασμό.");

        var now = DateTime.UtcNow;
        var v = await GetOrCreateAsync(user.Id);
        if (v.PhoneCodeSentAt != null && now - v.PhoneCodeSentAt < PhoneCodeCooldown)
            return (false, "Περιμένετε λίγο πριν ζητήσετε νέο κωδικό.");
        if (v.PhoneCodesCountDate?.Date != now.Date) { v.PhoneCodesCountDate = now; v.PhoneCodesSentToday = 0; }
        if (v.PhoneCodesSentToday >= MaxPhoneCodesPerDay)
            return (false, "Φτάσατε το ημερήσιο όριο αποστολής κωδικών. Δοκιμάστε αύριο.");

        var code = await _users.GenerateChangePhoneNumberTokenAsync(user, number);
        var ok = await _sms.SendAsync(number, $"frontida4all: ο κωδικός επιβεβαίωσής σας είναι {code}", CancellationToken.None);
        if (!ok) return (false, "Δεν στάλθηκε το SMS. Δοκιμάστε ξανά αργότερα.");

        v.PhoneCodeSentAt = now;
        v.PhoneCodesSentToday++;
        v.PhoneCodeFailedAttempts = 0;
        v.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> VerifyPhoneCodeAsync(ApplicationUser user, string phone, string code)
    {
        var number = NormalizePhone(phone);
        if (number is null || string.IsNullOrWhiteSpace(code)) return (false, "Συμπληρώστε αριθμό και κωδικό.");

        var v = await GetOrCreateAsync(user.Id);
        if (v.PhoneCodeFailedAttempts >= MaxPhoneFailedAttempts)
            return (false, "Πολλές λανθασμένες προσπάθειες. Ζητήστε νέο κωδικό.");

        var result = await _users.ChangePhoneNumberAsync(user, number, code.Trim());
        if (!result.Succeeded)
        {
            v.PhoneCodeFailedAttempts++;
            await _db.SaveChangesAsync();
            return (false, "Λάθος ή ληγμένος κωδικός.");
        }
        v.PhoneCodeFailedAttempts = 0;
        await _db.SaveChangesAsync();
        await SyncProfileAsync(user.Id);
        return (true, null);
    }

    // ── Identity (Stripe Identity) ───────────────────────────────────────────

    private bool StripeIdentityEnabled =>
        _cfg.GetValue<bool>("Stripe:Enabled") && !string.IsNullOrWhiteSpace(_cfg["Stripe:SecretKey"])
        && _cfg.GetValue("Stripe:IdentityEnabled", true);

    public async Task<(string? Url, string? Error)> StartIdentityCheckAsync(ApplicationUser user, string returnUrl)
    {
        if (!StripeIdentityEnabled) return (null, "Ο έλεγχος ταυτότητας δεν είναι διαθέσιμος αυτή τη στιγμή.");

        var v = await GetOrCreateAsync(user.Id);
        if (v.IdentityStatus == IdentityCheckStatus.Verified) return (null, "Η ταυτότητά σας έχει ήδη επαληθευτεί.");

        try
        {
            var client = new StripeClient(_cfg["Stripe:SecretKey"]);
            var service = new VerificationSessionService(client);
            var session = await service.CreateAsync(new VerificationSessionCreateOptions
            {
                Type = "document",
                ReturnUrl = returnUrl,
                Metadata = new Dictionary<string, string> { ["userId"] = user.Id },
                Options = new VerificationSessionOptionsOptions
                {
                    Document = new VerificationSessionOptionsDocumentOptions
                    {
                        RequireMatchingSelfie = true,
                        RequireLiveCapture = true,
                    },
                },
            });
            v.StripeIdentitySessionId = session.Id;
            v.IdentityStatus = IdentityCheckStatus.Pending;
            v.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (session.Url, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe Identity session creation failed for {UserId}", user.Id);
            return (null, "Δεν ήταν δυνατή η έναρξη του ελέγχου. Δοκιμάστε ξανά αργότερα.");
        }
    }

    public async Task RefreshIdentityStatusAsync(string userId)
    {
        var v = await _db.UserVerifications.FirstOrDefaultAsync(x => x.UserId == userId);
        if (v?.StripeIdentitySessionId is null || !StripeIdentityEnabled) return;
        if (v.IdentityStatus == IdentityCheckStatus.Verified) return;

        try
        {
            var client = new StripeClient(_cfg["Stripe:SecretKey"]);
            var session = await new VerificationSessionService(client).GetAsync(v.StripeIdentitySessionId);
            await ApplyIdentitySessionAsync(session);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe Identity refresh failed for {UserId}", userId);
        }
    }

    public async Task ApplyIdentitySessionAsync(VerificationSession session)
    {
        string? userId = null;
        session.Metadata?.TryGetValue("userId", out userId);
        if (string.IsNullOrEmpty(userId)) return;

        var v = await GetOrCreateAsync(userId);
        // Ignore events of an older session than the one currently tracked.
        if (v.StripeIdentitySessionId != null && v.StripeIdentitySessionId != session.Id) return;

        var now = DateTime.UtcNow;
        v.StripeIdentitySessionId = session.Id;
        switch (session.Status)
        {
            case "verified":
                v.IdentityStatus = IdentityCheckStatus.Verified;
                v.IdentityVerifiedAt ??= now;
                break;
            case "requires_input":
                // A session sits in requires_input before the user starts, and again after a failed attempt.
                v.IdentityStatus = session.LastError != null ? IdentityCheckStatus.Failed : IdentityCheckStatus.Pending;
                break;
            case "processing":
                v.IdentityStatus = IdentityCheckStatus.Pending;
                break;
            case "canceled":
                v.IdentityStatus = IdentityCheckStatus.Failed;
                break;
        }
        v.UpdatedAt = now;
        await _db.SaveChangesAsync();
        await SyncProfileAsync(userId);
    }
}
