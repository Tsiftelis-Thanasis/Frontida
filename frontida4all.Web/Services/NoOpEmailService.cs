namespace frontida4all.Web.Services;

public class NoOpEmailService : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody) => Task.CompletedTask;
}
