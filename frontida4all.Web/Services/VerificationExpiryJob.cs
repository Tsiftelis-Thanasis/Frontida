using frontida4all.Web.Data;
using frontida4all.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace frontida4all.Web.Services;

/// <summary>
/// Periodically checks approved verification documents: sends a reminder before they expire,
/// an "expired" notice afterwards, and removes the "verified" stamp from profiles that are no longer fully verified.
/// </summary>
public class VerificationExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<VerificationOptions> _opts;
    private readonly ILogger<VerificationExpiryJob> _logger;

    public VerificationExpiryJob(
        IServiceScopeFactory scopeFactory,
        IOptions<VerificationOptions> opts,
        ILogger<VerificationExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _opts = opts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "VerificationExpiryJob run failed.");
            }

            try { await Task.Delay(TimeSpan.FromHours(Math.Max(1, _opts.Value.ExpiryJobIntervalHours)), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    internal async Task RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var email = sp.GetRequiredService<IUserEmailService>();
        var verification = sp.GetRequiredService<IVerificationService>();

        var now = DateTime.UtcNow;
        var warnBefore = now.AddDays(_opts.Value.ExpiryWarningDays);

        var due = await db.UserVerifications
            .Include(v => v.User)
            .Where(v => (v.CriminalRecordExpiresAt != null && v.CriminalRecordExpiresAt <= warnBefore)
                     || (v.AddressExpiresAt != null && v.AddressExpiresAt <= warnBefore))
            .Where(v => v.User.IsCaregiver)
            .ToListAsync(ct);

        foreach (var v in due)
        {
            if (v.CriminalRecordExpiresAt is { } cr)
            {
                if (cr <= now)
                {
                    if (v.CriminalRecordExpiredNoticeSentAt == null)
                    {
                        await TrySendAsync(() => email.SendVerificationExpiryAsync(v.User, VerificationDocumentType.CriminalRecord, cr, true));
                        v.CriminalRecordExpiredNoticeSentAt = now;
                    }
                }
                else if (v.CriminalRecordWarningSentAt == null)
                {
                    await TrySendAsync(() => email.SendVerificationExpiryAsync(v.User, VerificationDocumentType.CriminalRecord, cr, false));
                    v.CriminalRecordWarningSentAt = now;
                }
            }

            if (v.AddressExpiresAt is { } ad)
            {
                if (ad <= now)
                {
                    if (v.AddressExpiredNoticeSentAt == null)
                    {
                        await TrySendAsync(() => email.SendVerificationExpiryAsync(v.User, VerificationDocumentType.AddressProof, ad, true));
                        v.AddressExpiredNoticeSentAt = now;
                    }
                }
                else if (v.AddressWarningSentAt == null)
                {
                    await TrySendAsync(() => email.SendVerificationExpiryAsync(v.User, VerificationDocumentType.AddressProof, ad, false));
                    v.AddressWarningSentAt = now;
                }
            }
        }
        await db.SaveChangesAsync(ct);

        // Drop the stamp wherever a verified profile is no longer fully verified (also covers failed emails).
        var verifiedProfiles = await db.Profiles.Where(p => p.IsVerified).Select(p => p.UserId).ToListAsync(ct);
        foreach (var userId in verifiedProfiles)
            await verification.SyncProfileAsync(userId);
    }

    private async Task TrySendAsync(Func<Task> send)
    {
        try { await send(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Verification expiry email failed."); }
    }
}
