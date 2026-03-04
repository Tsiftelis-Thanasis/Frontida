using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

public interface ISubscriptionService
{
    Task<SubscriptionPlan> GetPlanAsync(string userId);
    Task<bool> CanPostAsync(string userId);
    Task<bool> CanReplyAsync(string userId);
    Task<bool> CanReactAsync(string userId);
    Task<bool> IsPhoneVisibleAsync(string viewerUserId);
    Task<bool> CanViewProfileAsync(string caregiverUserId, string targetUserId);
}
