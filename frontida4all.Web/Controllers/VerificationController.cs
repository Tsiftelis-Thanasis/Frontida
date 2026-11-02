using frontida4all.Web.Data;
using frontida4all.Web.Models.Entities;
using frontida4all.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace frontida4all.Web.Controllers;

public class VerificationPageViewModel
{
    public VerificationSummary Summary { get; set; } = new();
    public string? Address { get; set; }
    public bool SmsAvailable { get; set; }
    public bool IdentityAvailable { get; set; }
    public int CriminalRecordValidityMonths { get; set; }
    public int AddressValidityMonths { get; set; }
    public int MaxFileSizeMb { get; set; }
}

[Authorize]
[Route("verification")]
public class VerificationController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IVerificationService _verification;
    private readonly ISmsSender _sms;
    private readonly IConfiguration _cfg;
    private readonly VerificationOptions _opts;
    private readonly IAppLogService _log;

    public VerificationController(
        UserManager<ApplicationUser> users,
        IVerificationService verification,
        ISmsSender sms,
        IConfiguration cfg,
        IOptions<VerificationOptions> opts,
        IAppLogService log)
    {
        _users = users; _verification = verification; _sms = sms; _cfg = cfg; _opts = opts.Value; _log = log;
    }

    private async Task<ApplicationUser?> CaregiverAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user is { IsCaregiver: true } ? user : null;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await CaregiverAsync();
        if (user is null) return RedirectToAction("Index", "Dashboard");

        await _verification.RefreshIdentityStatusAsync(user.Id);
        await _verification.SyncProfileAsync(user.Id);

        return View(new VerificationPageViewModel
        {
            Summary = await _verification.GetSummaryAsync(user.Id),
            Address = string.Join(", ", new[] { user.Address, user.City, user.PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s))),
            SmsAvailable = _sms.IsAvailable,
            IdentityAvailable = _cfg.GetValue<bool>("Stripe:Enabled") && _cfg.GetValue("Stripe:IdentityEnabled", true),
            CriminalRecordValidityMonths = _opts.CriminalRecordValidityMonths,
            AddressValidityMonths = _opts.AddressProofValidityMonths,
            MaxFileSizeMb = _opts.MaxFileSizeMb,
        });
    }

    [HttpPost("phone/send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPhoneCode(string phone)
    {
        var user = await CaregiverAsync();
        if (user is null) return Forbid();

        var (ok, error) = await _verification.SendPhoneCodeAsync(user, phone);
        if (ok)
        {
            TempData["VerifyPhone"] = phone;
            TempData["VerifyMessage"] = "Στείλαμε κωδικό με SMS. Γράψτε τον παρακάτω.";
        }
        else TempData["VerifyError"] = error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("phone/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPhone(string phone, string code)
    {
        var user = await CaregiverAsync();
        if (user is null) return Forbid();

        var (ok, error) = await _verification.VerifyPhoneCodeAsync(user, phone, code);
        if (ok) TempData["VerifyMessage"] = "Το κινητό σας επιβεβαιώθηκε.";
        else
        {
            TempData["VerifyError"] = error;
            TempData["VerifyPhone"] = phone;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("address")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadAddress(IFormFile file)
    {
        var user = await CaregiverAsync();
        if (user is null) return Forbid();
        if (string.IsNullOrWhiteSpace(user.Address) || string.IsNullOrWhiteSpace(user.City))
        {
            TempData["VerifyError"] = "Συμπληρώστε πρώτα τη διεύθυνση και την πόλη στις Ρυθμίσεις προφίλ.";
            return RedirectToAction(nameof(Index));
        }

        var (ok, error) = await _verification.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, file, null);
        if (ok)
        {
            await _log.LogAsync(AppLogLevel.Info, "Verification", "Address proof uploaded", userId: user.Id);
            TempData["VerifyMessage"] = "Το έγγραφο στάλθηκε για έλεγχο.";
        }
        else TempData["VerifyError"] = error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("criminal-record")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadCriminalRecord(IFormFile file, DateTime? issuedOn)
    {
        var user = await CaregiverAsync();
        if (user is null) return Forbid();

        var (ok, error) = await _verification.SubmitDocumentAsync(user.Id, VerificationDocumentType.CriminalRecord, file, issuedOn);
        if (ok)
        {
            await _log.LogAsync(AppLogLevel.Info, "Verification", "Criminal record certificate uploaded", userId: user.Id);
            TempData["VerifyMessage"] = "Το πιστοποιητικό στάλθηκε για έλεγχο.";
        }
        else TempData["VerifyError"] = error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("identity/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartIdentity()
    {
        var user = await CaregiverAsync();
        if (user is null) return Forbid();

        var returnUrl = Url.Action(nameof(IdentityReturn), "Verification", null, Request.Scheme)!;
        var (url, error) = await _verification.StartIdentityCheckAsync(user, returnUrl);
        if (url is null)
        {
            TempData["VerifyError"] = error;
            return RedirectToAction(nameof(Index));
        }
        return Redirect(url);
    }

    [HttpGet("identity/return")]
    public async Task<IActionResult> IdentityReturn()
    {
        var user = await CaregiverAsync();
        if (user is null) return RedirectToAction("Index", "Dashboard");

        await _verification.RefreshIdentityStatusAsync(user.Id);
        var summary = await _verification.GetSummaryAsync(user.Id);
        TempData["VerifyMessage"] = summary.IdentityVerified
            ? "Η ταυτότητά σας επαληθεύτηκε."
            : "Λάβαμε τα στοιχεία σας. Θα ενημερωθείτε μόλις ολοκληρωθεί ο έλεγχος.";
        return RedirectToAction(nameof(Index));
    }
}
