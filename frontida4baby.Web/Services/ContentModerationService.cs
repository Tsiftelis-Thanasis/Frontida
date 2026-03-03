using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;

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

    public ContentModerationService(
        WordlistModerationService wordlist,
        ClaudeModerationService   claude,
        ApplicationDbContext      db)
    {
        _wordlist = wordlist;
        _claude   = claude;
        _db       = db;
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
    }
}
