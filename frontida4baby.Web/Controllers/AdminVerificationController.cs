using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Controllers;

public class VerificationQueueItem
{
    public int DocumentId { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public VerificationDocumentType Type { get; set; }
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? IssuedOn { get; set; }
    public bool HasFile { get; set; }
}

[Authorize(Roles = "Admin")]
[Route("admin/verification")]
public class AdminVerificationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IVerificationService _verification;
    private readonly IAppLogService _log;

    public AdminVerificationController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IVerificationService verification,
        IAppLogService log)
    {
        _db = db; _users = users; _verification = verification; _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Queue()
    {
        var items = await _db.VerificationDocuments.AsNoTracking()
            .Where(d => d.Status == DocumentReviewStatus.Pending)
            .OrderBy(d => d.UploadedAt)
            .Select(d => new VerificationQueueItem
            {
                DocumentId = d.Id,
                UserId = d.UserId,
                UserName = ((d.User.FirstName ?? "") + " " + (d.User.LastName ?? "")).Trim(),
                Email = d.User.Email ?? "",
                Address = d.User.Address + ", " + d.User.City + " " + d.User.PostalCode,
                Type = d.Type,
                FileName = d.FileName,
                FileSize = d.FileSize,
                UploadedAt = d.UploadedAt,
                IssuedOn = d.IssuedOn,
                HasFile = d.Data != null,
            })
            .ToListAsync();
        return View(items);
    }

    /// <summary>Streams the private document to the reviewer. Never cached, never indexed.</summary>
    [HttpGet("file/{id:int}")]
    public async Task<IActionResult> File(int id)
    {
        var doc = await _db.VerificationDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc?.Data is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self' data:; object-src 'self'; style-src 'unsafe-inline'";
        await _log.LogAsync(AppLogLevel.Info, "Verification",
            $"Admin viewed document {id} ({doc.Type})", userId: _users.GetUserId(User));
        return File(doc.Data, doc.ContentType);
    }

    [HttpPost("review/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool approve, DateTime? issuedOn, string? reason)
    {
        var (ok, error) = await _verification.ReviewDocumentAsync(
            id, _users.GetUserId(User)!, approve, issuedOn, reason);

        TempData[ok ? "AdminMessage" : "AdminError"] = ok
            ? (approve ? "Εγκρίθηκε." : "Απορρίφθηκε.")
            : error;
        if (ok)
            await _log.LogAsync(AppLogLevel.Info, "Verification",
                $"Document {id} {(approve ? "approved" : "rejected")}", userId: _users.GetUserId(User));
        return RedirectToAction(nameof(Queue));
    }
}
