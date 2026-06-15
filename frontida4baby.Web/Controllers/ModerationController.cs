using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;

namespace frontida4baby.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ModerationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ContentModerationService _moderationLog;

    public ModerationController(ApplicationDbContext db, ContentModerationService moderationLog)
    {
        _db = db;
        _moderationLog = moderationLog;
    }

    // ── GET /moderation ───────────────────────────────────────────────────────
    public async Task<IActionResult> Queue()
    {
        var pendingPosts = await _db.Posts
            .Include(p => p.AuthorUser)
            .Where(p => p.ModerationStatus == ModerationStatus.PendingReview)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PendingPostViewModel
            {
                Id         = p.Id,
                Title      = p.Title,
                Body       = p.Body,
                AuthorName = $"{p.AuthorUser.FirstName} {p.AuthorUser.LastName}".Trim(),
                City       = p.City,
                ServiceType = p.ServiceType,
                CreatedAt  = p.CreatedAt,
            })
            .ToListAsync();

        var pendingReplies = await _db.Replies
            .Include(r => r.AuthorUser)
            .Include(r => r.Post)
            .Where(r => r.ModerationStatus == ModerationStatus.PendingReview)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new PendingReplyViewModel
            {
                Id         = r.Id,
                PostId     = r.PostId,
                PostTitle  = r.Post.Title,
                Body       = r.Body,
                AuthorName = $"{r.AuthorUser.FirstName} {r.AuthorUser.LastName}".Trim(),
                CreatedAt  = r.CreatedAt,
            })
            .ToListAsync();

        return View(new AdminQueueViewModel
        {
            PendingPosts   = pendingPosts,
            PendingReplies = pendingReplies,
        });
    }

    // ── POST /moderation/approve ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ContentType contentType, int id, string? returnUrl = null)
    {
        var result = new ModerationResult(ModerationStatus.Approved, "Manual approval by admin", null, ModerationStage.Manual, 1f);

        if (contentType == ContentType.Post)
        {
            var post = await _db.Posts.FindAsync(id);
            if (post is not null)
            {
                post.ModerationStatus = ModerationStatus.Approved;
                await _db.SaveChangesAsync();
                await _moderationLog.LogAsync(ContentType.Post, id, post.AuthorUserId,
                    post.Title, post.Body, result);
            }
        }
        else
        {
            var reply = await _db.Replies.FindAsync(id);
            if (reply is not null)
            {
                reply.ModerationStatus = ModerationStatus.Approved;
                await _db.SaveChangesAsync();
                await _moderationLog.LogAsync(ContentType.Reply, id, reply.AuthorUserId,
                    null, reply.Body, result);
            }
        }

        TempData["AdminMessage"] = "Item approved.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Queue));
    }

    // ── POST /moderation/reject ───────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ContentType contentType, int id, string reason, string? returnUrl = null)
    {
        var result = new ModerationResult(ModerationStatus.Rejected, reason, null, ModerationStage.Manual, 1f);

        if (contentType == ContentType.Post)
        {
            var post = await _db.Posts.FindAsync(id);
            if (post is not null)
            {
                post.ModerationStatus = ModerationStatus.Rejected;
                post.ModerationReason = reason;
                post.Status           = PostStatus.Deleted;
                await _db.SaveChangesAsync();
                await _moderationLog.LogAsync(ContentType.Post, id, post.AuthorUserId,
                    post.Title, post.Body, result);
            }
        }
        else
        {
            var reply = await _db.Replies.FindAsync(id);
            if (reply is not null)
            {
                reply.ModerationStatus = ModerationStatus.Rejected;
                reply.ModerationReason = reason;
                await _db.SaveChangesAsync();
                await _moderationLog.LogAsync(ContentType.Reply, id, reply.AuthorUserId,
                    null, reply.Body, result);
            }
        }

        TempData["AdminMessage"] = "Item rejected.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Queue));
    }

    // ── GET /moderation/logs ──────────────────────────────────────────────────
    public async Task<IActionResult> Logs()
    {
        var logs = await _db.ModerationLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync();

        return View(logs);
    }
}
