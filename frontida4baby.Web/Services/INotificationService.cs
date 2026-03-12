namespace frontida4baby.Web.Services;

public interface INotificationService
{
    Task NotifyServerErrorAsync(string path, Exception ex);
    Task NotifyNewRegistrationAsync(string email);
    Task NotifyUserBlacklistedAsync(string email, string userId, string reason);
    Task NotifyPostRejectedAsync(string authorEmail, string authorId, string contentSnippet, string rejectionReason);
    Task NotifySupportRequestAsync(string name, string email, string subject, string category, string message);
}
