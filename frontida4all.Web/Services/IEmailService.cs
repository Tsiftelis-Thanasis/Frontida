namespace frontida4all.Web.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}
