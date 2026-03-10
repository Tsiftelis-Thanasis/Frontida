using Microsoft.Extensions.Options;

namespace frontida4baby.Web.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService        _email;
    private readonly EmailOptions         _emailOpts;
    private readonly NotificationOptions  _notifOpts;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEmailService email,
        IOptions<EmailOptions> emailOpts,
        IOptions<NotificationOptions> notifOpts,
        ILogger<NotificationService> logger)
    {
        _email      = email;
        _emailOpts  = emailOpts.Value;
        _notifOpts  = notifOpts.Value;
        _logger     = logger;
    }

    public async Task NotifyServerErrorAsync(string path, Exception ex)
    {
        if (!_notifOpts.ServerErrors) return;

        var html = $"""
            <div style="font-family:monospace;max-width:700px">
              <h2 style="color:#dc2626">⚠️ Server Error — frontida4all</h2>
              <table style="border-collapse:collapse;width:100%;margin-bottom:16px">
                <tr><td style="padding:6px 12px;background:#fee2e2;font-weight:bold;width:120px">Time</td>
                    <td style="padding:6px 12px;background:#fef2f2">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
                <tr><td style="padding:6px 12px;background:#fee2e2;font-weight:bold">Path</td>
                    <td style="padding:6px 12px;background:#fef2f2">{System.Net.WebUtility.HtmlEncode(path)}</td></tr>
                <tr><td style="padding:6px 12px;background:#fee2e2;font-weight:bold">Error</td>
                    <td style="padding:6px 12px;background:#fef2f2">{System.Net.WebUtility.HtmlEncode(ex.Message)}</td></tr>
              </table>
              <pre style="background:#1e1e1e;color:#d4d4d4;padding:16px;border-radius:6px;overflow-x:auto;font-size:12px">{System.Net.WebUtility.HtmlEncode(ex.ToString())}</pre>
            </div>
            """;

        await SendToAdminAsync("Server Error", ex.GetType().Name, Truncate(ex.Message, 80), html);
    }

    public async Task NotifyNewRegistrationAsync(string email)
    {
        if (!_notifOpts.NewRegistration) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px">
              <h2 style="color:#4f46e5">👤 New Registration — frontida4all</h2>
              <p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(email)}</p>
              <p style="color:#6b7280;font-size:13px">Registered at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            </div>
            """;

        await SendToAdminAsync("New User", email, null, html);
    }

    public async Task NotifyUserBlacklistedAsync(string email, string userId, string reason)
    {
        if (!_notifOpts.UserBlacklisted) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px">
              <h2 style="color:#d97706">🚫 User Auto-Blacklisted — frontida4all</h2>
              <table style="border-collapse:collapse;width:100%">
                <tr><td style="padding:6px 12px;background:#fef3c7;font-weight:bold;width:120px">Email</td>
                    <td style="padding:6px 12px;background:#fffbeb">{System.Net.WebUtility.HtmlEncode(email)}</td></tr>
                <tr><td style="padding:6px 12px;background:#fef3c7;font-weight:bold">User ID</td>
                    <td style="padding:6px 12px;background:#fffbeb"><code>{userId}</code></td></tr>
                <tr><td style="padding:6px 12px;background:#fef3c7;font-weight:bold">Reason</td>
                    <td style="padding:6px 12px;background:#fffbeb">{System.Net.WebUtility.HtmlEncode(reason)}</td></tr>
                <tr><td style="padding:6px 12px;background:#fef3c7;font-weight:bold">Time</td>
                    <td style="padding:6px 12px;background:#fffbeb">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
              </table>
              <p style="color:#6b7280;font-size:13px;margin-top:16px">
                To unblacklist: Admin → Users → find user → Άρση Αποκλεισμού
              </p>
            </div>
            """;

        await SendToAdminAsync("User Blacklisted", email, null, html);
    }

    public async Task NotifyPostRejectedAsync(string authorEmail, string authorId, string contentSnippet, string rejectionReason)
    {
        if (!_notifOpts.PostRejected) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px">
              <h2 style="color:#6b7280">🛑 Content Rejected — frontida4all</h2>
              <table style="border-collapse:collapse;width:100%">
                <tr><td style="padding:6px 12px;background:#f3f4f6;font-weight:bold;width:120px">Author</td>
                    <td style="padding:6px 12px">{System.Net.WebUtility.HtmlEncode(authorEmail)}</td></tr>
                <tr><td style="padding:6px 12px;background:#f3f4f6;font-weight:bold">Reason</td>
                    <td style="padding:6px 12px">{System.Net.WebUtility.HtmlEncode(rejectionReason)}</td></tr>
                <tr><td style="padding:6px 12px;background:#f3f4f6;font-weight:bold">Content</td>
                    <td style="padding:6px 12px"><em>{System.Net.WebUtility.HtmlEncode(Truncate(contentSnippet, 200))}</em></td></tr>
              </table>
            </div>
            """;

        await SendToAdminAsync("Content Rejected", authorEmail, Truncate(contentSnippet, 60), html);
    }

    // ─────────────────────────────────────────────────────────────────────────

    // subject format:  "frontida4all | Category: detail"
    //                  "frontida4all | Category"          (when detail is null)
    private async Task SendToAdminAsync(string category, string primary, string? detail, string html)
    {
        var appName = string.IsNullOrWhiteSpace(_emailOpts.FromName) ? "frontida4all" : _emailOpts.FromName;
        var subject = detail is null
            ? $"{appName} | {category}: {primary}"
            : $"{appName} | {category}: {primary} — {detail}";

        var to = _emailOpts.AdminEmail;
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("Notification skipped — Email:AdminEmail is not configured. Subject: {Subject}", subject);
            return;
        }

        try
        {
            await _email.SendAsync(to, subject, html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification: {Subject}", subject);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
