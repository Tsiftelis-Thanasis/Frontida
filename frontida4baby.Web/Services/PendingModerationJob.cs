using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Services;

/// <summary>
/// Background job that re-processes posts and replies stuck in PendingReview
/// (e.g. saved before the Claude API key was configured, or that timed out).
/// Runs once at startup (after a 5-second delay) then every 5 minutes.
/// Only Stage 2 (Claude) is re-run — items already passed Stage 1 (wordlist).
/// </summary>
public class PendingModerationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingModerationJob> _logger;

    public PendingModerationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingModerationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingModerationJob: starting — initial delay 5 s.");

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessPendingAsync(scope, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PendingModerationJob: unhandled error in run.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(IServiceScope scope, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var claude = scope.ServiceProvider.GetRequiredService<ClaudeModerationService>();
        var moderation = scope.ServiceProvider.GetRequiredService<ContentModerationService>();

        var pendingPosts = await db.Posts
            .Where(p => p.ModerationStatus == ModerationStatus.PendingReview)
            .Select(p => new { p.Id, p.AuthorUserId, p.Title, p.Body })
            .AsNoTracking()
            .ToListAsync(ct);

        var pendingReplies = await db.Replies
            .Where(r => r.ModerationStatus == ModerationStatus.PendingReview)
            .Select(r => new { r.Id, r.AuthorUserId, r.Body })
            .AsNoTracking()
            .ToListAsync(ct);

        _logger.LogInformation(
            "PendingModerationJob: found {PostCount} pending post(s), {ReplyCount} pending reply(ies).",
            pendingPosts.Count, pendingReplies.Count);

        if (pendingPosts.Count == 0 && pendingReplies.Count == 0)
            return;

        // ── Posts ─────────────────────────────────────────────────────────────
        foreach (var item in pendingPosts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await claude.CheckAsync(item.Title, item.Body);

                var post = await db.Posts.FindAsync([item.Id], ct);
                if (post is null) continue;

                post.ModerationStatus = result.Status;
                post.ModerationReason = result.Reason;

                await moderation.LogAsync(
                    ContentType.Post, item.Id, item.AuthorUserId,
                    item.Title, item.Body, result);

                _logger.LogInformation(
                    "PendingModerationJob: Post {Id} → {Status} ({Reason}).",
                    item.Id, result.Status, result.Reason ?? "—");

                await Task.Delay(200, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "PendingModerationJob: error processing Post {Id}.", item.Id);
            }
        }

        // ── Replies ───────────────────────────────────────────────────────────
        foreach (var item in pendingReplies)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await claude.CheckAsync(null, item.Body);

                var reply = await db.Replies.FindAsync([item.Id], ct);
                if (reply is null) continue;

                reply.ModerationStatus = result.Status;
                reply.ModerationReason = result.Reason;

                await moderation.LogAsync(
                    ContentType.Reply, item.Id, item.AuthorUserId,
                    null, item.Body, result);

                _logger.LogInformation(
                    "PendingModerationJob: Reply {Id} → {Status} ({Reason}).",
                    item.Id, result.Status, result.Reason ?? "—");

                await Task.Delay(200, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "PendingModerationJob: error processing Reply {Id}.", item.Id);
            }
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PendingModerationJob: run complete — {PostCount} post(s) and {ReplyCount} reply(ies) processed.",
            pendingPosts.Count, pendingReplies.Count);
    }
}
