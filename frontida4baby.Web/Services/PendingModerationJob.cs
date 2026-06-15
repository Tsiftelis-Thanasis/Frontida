using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Services;

/// <summary>
/// Background job that re-processes posts and replies stuck in PendingReview.
/// Caps at 50 items per run and 5 attempts per item. Backs off on consecutive failures.
/// </summary>
public class PendingModerationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingModerationJob> _logger;
    private const int MaxAttemptsPerItem = 5;
    private const int MaxItemsPerRun = 50;

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
            int consecutiveFailures = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                consecutiveFailures = await ProcessPendingAsync(scope, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PendingModerationJob: unhandled error in run.");
                consecutiveFailures = MaxItemsPerRun; // treat as full failure
            }

            // Backoff: if many failures, wait longer (up to 30 min)
            var delay = consecutiveFailures > 5
                ? TimeSpan.FromMinutes(Math.Min(30, consecutiveFailures * 2))
                : TimeSpan.FromMinutes(5);

            await Task.Delay(delay, stoppingToken);
        }
    }

    /// <returns>Number of consecutive API failures in this run (for backoff).</returns>
    private async Task<int> ProcessPendingAsync(IServiceScope scope, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var claude = scope.ServiceProvider.GetRequiredService<ClaudeModerationService>();
        var moderation = scope.ServiceProvider.GetRequiredService<ContentModerationService>();

        var pendingPosts = await db.Posts
            .Where(p => p.ModerationStatus == ModerationStatus.PendingReview
                     && p.ModerationAttempts < MaxAttemptsPerItem)
            .OrderBy(p => p.CreatedAt)
            .Take(MaxItemsPerRun)
            .Select(p => new { p.Id, p.AuthorUserId, p.Title, p.Body })
            .AsNoTracking()
            .ToListAsync(ct);

        var remainingSlots = MaxItemsPerRun - pendingPosts.Count;

        var pendingReplies = remainingSlots > 0
            ? await db.Replies
                .Where(r => r.ModerationStatus == ModerationStatus.PendingReview
                         && r.ModerationAttempts < MaxAttemptsPerItem)
                .OrderBy(r => r.CreatedAt)
                .Take(remainingSlots)
                .Select(r => new { r.Id, r.AuthorUserId, r.Body })
                .AsNoTracking()
                .ToListAsync(ct)
            : [];

        _logger.LogInformation(
            "PendingModerationJob: found {PostCount} pending post(s), {ReplyCount} pending reply(ies).",
            pendingPosts.Count, pendingReplies.Count);

        if (pendingPosts.Count == 0 && pendingReplies.Count == 0)
            return 0;

        int consecutiveFailures = 0;

        // ── Posts ─────────────────────────────────────────────────────────────
        foreach (var item in pendingPosts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await claude.CheckAsync(item.Title, item.Body);

                var post = await db.Posts.FindAsync([item.Id], ct);
                if (post is null) continue;

                post.ModerationAttempts++;
                post.ModerationStatus = result.Status;
                post.ModerationReason = result.Reason;

                // Use a new scope for LogAsync to avoid disposed-scope issues in fire-and-forget notifications
                using (var logScope = _scopeFactory.CreateScope())
                {
                    var logModeration = logScope.ServiceProvider.GetRequiredService<ContentModerationService>();
                    await logModeration.LogAsync(
                        ContentType.Post, item.Id, item.AuthorUserId,
                        item.Title, item.Body, result);
                }

                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "PendingModerationJob: Post {Id} → {Status} (attempt {Attempt}).",
                    item.Id, result.Status, post.ModerationAttempts);

                if (result.Status != ModerationStatus.PendingReview)
                    consecutiveFailures = 0;
                else
                    consecutiveFailures++;

                await Task.Delay(200, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                consecutiveFailures++;
                _logger.LogWarning(ex, "PendingModerationJob: error processing Post {Id}.", item.Id);

                // Increment attempts even on failure
                var post = await db.Posts.FindAsync([item.Id], ct);
                if (post is not null)
                {
                    post.ModerationAttempts++;
                    await db.SaveChangesAsync(ct);
                }
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

                reply.ModerationAttempts++;
                reply.ModerationStatus = result.Status;
                reply.ModerationReason = result.Reason;

                using (var logScope = _scopeFactory.CreateScope())
                {
                    var logModeration = logScope.ServiceProvider.GetRequiredService<ContentModerationService>();
                    await logModeration.LogAsync(
                        ContentType.Reply, item.Id, item.AuthorUserId,
                        null, item.Body, result);
                }

                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "PendingModerationJob: Reply {Id} → {Status} (attempt {Attempt}).",
                    item.Id, result.Status, reply.ModerationAttempts);

                if (result.Status != ModerationStatus.PendingReview)
                    consecutiveFailures = 0;
                else
                    consecutiveFailures++;

                await Task.Delay(200, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                consecutiveFailures++;
                _logger.LogWarning(ex, "PendingModerationJob: error processing Reply {Id}.", item.Id);

                var reply = await db.Replies.FindAsync([item.Id], ct);
                if (reply is not null)
                {
                    reply.ModerationAttempts++;
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        _logger.LogInformation(
            "PendingModerationJob: run complete — {PostCount} post(s) and {ReplyCount} reply(ies) processed.",
            pendingPosts.Count, pendingReplies.Count);

        return consecutiveFailures;
    }
}
