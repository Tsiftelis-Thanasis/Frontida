namespace frontida4baby.Web.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}
