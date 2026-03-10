using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace frontida4baby.Web.Services;

/// <summary>
/// Orchestrates the two-stage moderation pipeline:
///   Stage 1 — WordlistModerationService (instant, offline)
///   Stage 2 — ClaudeModerationService (semantic, async)
/// Also persists every decision to ModerationLog for audit purposes.
/// </summary>
public class ContentModerationService : IContentModerationService
{
    private readonly WordlistModerationService _wordlist;
    private readonly ClaudeModerationService   _claude;
    private readonly ApplicationDbContext      _db;
    private readonly INotificationService      _notifications;
    private readonly int                       _blacklistThreshold;

    public ContentModerationService(
        WordlistModerationService wordlist,
        ClaudeModerationService   claude,
        ApplicationDbContext      db,
        INotificationService      notifications,
        IOptions<ModerationOptions> options)
    {
        _wordlist           = wordlist;
        _claude             = claude;
        _db                 = db;
        _notifications      = notifications;
        _blacklistThreshold = options.Value.BlacklistThreshold;
    }

    public async Task<ModerationResult> ModerateAsync(string? title, string body)
    {
        // Stage 1 — wordlist (synchronous, ~0 ms)
        var result = _wordlist.Check(title, body);

        // Stage 2 — Claude (only if wordlist passed)
        if (result.Status == ModerationStatus.Approved)
            result = await _claude.CheckAsync(title, body);

        return result;
    }

    /// <summary>
    /// Persists the moderation decision to the audit log.
    /// Call this after saving the Post/Reply so ContentId is available.
    /// If rejected, checks if the author has hit the blacklist threshold.
    /// </summary>
    public async Task LogAsync(
        ContentType      contentType,
        int              contentId,
        string           authorUserId,
        string?          title,
        string           body,
        ModerationResult result)
    {
        _db.ModerationLogs.Add(new ModerationLog
        {
            ContentType       = contentType,
            ContentId         = contentId,
            AuthorUserId      = authorUserId,
            OriginalContent   = $"{title}\n\n{body}".Trim(),
            Stage             = result.Stage,
            Decision          = result.Status,
            Reason            = result.Reason,
            ViolationCategory = result.ViolationCategory,
            ConfidenceScore   = result.Confidence,
        });
        await _db.SaveChangesAsync();

        if (result.Status == ModerationStatus.Rejected)
        {
            await CheckAndBlacklistAsync(authorUserId, title, body, result.Reason);

            // Notify admin of rejection (controlled by Notifications:PostRejected toggle)
            var user = await _db.Users.FindAsync(authorUserId);
            if (user is not null)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _notifications.NotifyPostRejectedAsync(
                            user.Email ?? authorUserId,
                            authorUserId,
                            $"{title}\n{body}",
                            result.Reason ?? "");
                    }
                    catch { /* best-effort */ }
                });
        }
    }

    private async Task CheckAndBlacklistAsync(string userId, string? title, string body, string? reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.IsBlacklisted) return;

        var rejectedCount = await _db.ModerationLogs
            .CountAsync(l => l.AuthorUserId == userId && l.Decision == ModerationStatus.Rejected);

        if (rejectedCount >= _blacklistThreshold)
        {
            var blacklistReason = "Auto: exceeded rejection threshold";
            user.IsBlacklisted   = true;
            user.BlacklistReason = blacklistReason;
            user.BlacklistedAt   = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _notifications.NotifyUserBlacklistedAsync(
                        user.Email ?? userId, userId, blacklistReason);
                }
                catch { /* best-effort */ }
            });
        }
    }
}
