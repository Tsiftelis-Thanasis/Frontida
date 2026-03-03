using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace frontida4baby.Web.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _opts;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> opts, ILogger<SmtpEmailService> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_opts.SmtpUser))
            {
                _logger.LogWarning("Email not configured — skipping send to {To}", to);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_opts.SmtpHost, _opts.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_opts.SmtpUser, _opts.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }
}
