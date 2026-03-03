using frontida4baby.Tests.Helpers;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace frontida4baby.Tests;

public class ReactionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ReactionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubscriptionService_FreeUser_CanReactUnderLimit()
    {
        using var scope = _factory.Services.CreateScope();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider,
            "reactuser@test.com", "Test1234!");

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.True(await svc.CanReactAsync(user.Id));
    }

    [Fact]
    public async Task SubscriptionService_FreeUserAtReactionLimit_CanReactReturnsFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider,
            "reactlimit@test.com", "Test1234!");

        // Create a dummy post
        var post = new Post
        {
            AuthorUserId     = user.Id,
            Title            = "Reaction test post",
            Body             = "Test body content for reaction test",
            ModerationStatus = ModerationStatus.Approved,
            Status           = PostStatus.Active,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        // Add 10 reactions this month
        for (int i = 0; i < 10; i++)
        {
            db.PostReactions.Add(new PostReaction
            {
                PostId    = post.Id,
                UserId    = user.Id,
                CreatedAt = DateTime.UtcNow,
            });
            // Each reaction needs different post to avoid unique constraint
            // In practice reactions are per (PostId, UserId), so we need 10 posts
            var extraPost = new Post
            {
                AuthorUserId     = user.Id,
                Title            = $"Extra post {i}",
                Body             = "Extra post body content here",
                ModerationStatus = ModerationStatus.Approved,
                Status           = PostStatus.Active,
            };
            db.Posts.Add(extraPost);
            await db.SaveChangesAsync();
            // Replace the reaction with one on the extra post
            var lastReaction = db.PostReactions.OrderByDescending(r => r.Id).First();
            db.PostReactions.Remove(lastReaction);
            db.PostReactions.Add(new PostReaction
            {
                PostId    = extraPost.Id,
                UserId    = user.Id,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.False(await svc.CanReactAsync(user.Id));
    }

    [Fact]
    public async Task SubscriptionService_FreeUser_PhoneIsNotVisible()
    {
        using var scope = _factory.Services.CreateScope();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider,
            "nophone@test.com", "Test1234!");

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.False(await svc.IsPhoneVisibleAsync(user.Id));
    }

    [Fact]
    public async Task SubscriptionService_PaidUser_PhoneIsVisible()
    {
        using var scope = _factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider,
            "paidphone@test.com", "Test1234!");

        db.Subscriptions.Add(new Subscription
        {
            UserId    = user.Id,
            Plan      = SubscriptionPlan.Paid,
            Status    = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.True(await svc.IsPhoneVisibleAsync(user.Id));
    }
}
