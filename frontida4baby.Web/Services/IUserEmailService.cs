using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

public interface IUserEmailService
{
    Task SendWelcomeAsync(ApplicationUser user);
    Task SendContentRejectedAsync(ApplicationUser user, string contentSnippet, string reason);
    Task SendReactionApprovedAsync(ApplicationUser caregiver, string postTitle, ApplicationUser op);
    Task SendPaymentSucceededAsync(ApplicationUser user);
    Task SendPaymentFailedAsync(ApplicationUser user);
    Task SendSubscriptionCancelledAsync(ApplicationUser user);
    Task SendSupportConfirmationAsync(string toEmail, string name, string subject);
    Task SendRefundIssuedAsync(ApplicationUser user, decimal amount, string currency);
    Task SendTicketReplyAsync(SupportTicket ticket, string replyBody);
}
