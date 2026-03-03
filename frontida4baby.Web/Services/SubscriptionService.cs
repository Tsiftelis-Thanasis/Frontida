using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;

    public SubscriptionService(ApplicationDbContext db) => _db = db;

    public async Task<SubscriptionPlan> GetPlanAsync(string userId)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (sub == null || sub.Status != SubscriptionStatus.Active) return SubscriptionPlan.Free;
        return sub.Plan;
    }

    public async Task<bool> CanPostAsync(string userId)
    {
        var plan = await GetPlanAsync(userId);
        if (plan == SubscriptionPlan.Paid) return true;
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var count = await _db.Posts.CountAsync(p => p.AuthorUserId == userId && p.CreatedAt >= start);
        return count < 5;
    }

    public async Task<bool> CanReplyAsync(string userId)
    {
        var plan = await GetPlanAsync(userId);
        if (plan == SubscriptionPlan.Paid) return true;
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var count = await _db.Replies.CountAsync(r => r.AuthorUserId == userId && r.CreatedAt >= start);
        return count < 20;
    }

    public async Task<bool> CanReactAsync(string userId)
    {
        var plan = await GetPlanAsync(userId);
        if (plan == SubscriptionPlan.Paid) return true;
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var count = await _db.PostReactions.CountAsync(r => r.UserId == userId && r.CreatedAt >= start);
        return count < 10;
    }

    public async Task<bool> IsPhoneVisibleAsync(string viewerUserId)
    {
        if (string.IsNullOrEmpty(viewerUserId)) return false;
        var plan = await GetPlanAsync(viewerUserId);
        return plan == SubscriptionPlan.Paid;
    }
}
