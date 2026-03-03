using frontida4baby.Tests.Helpers;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace frontida4baby.Tests;

public class PostTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PostTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPostsIndex_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/posts");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SubscriptionService_FreeUserExceedsPostLimit_CanPostReturnsFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider, "limituser@test.com", "Test1234!");

        // Add 5 posts this month
        for (int i = 0; i < 5; i++)
        {
            db.Posts.Add(new Post
            {
                AuthorUserId     = user.Id,
                Title            = $"Post {i}",
                Body             = "Test body content here",
                ModerationStatus = ModerationStatus.Approved,
                Status           = PostStatus.Active,
                CreatedAt        = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.False(await svc.CanPostAsync(user.Id));
    }

    [Fact]
    public async Task SubscriptionService_FreeUserUnderLimit_CanPostReturnsTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider, "underuser@test.com", "Test1234!");

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.True(await svc.CanPostAsync(user.Id));
    }

    [Fact]
    public async Task SubscriptionService_PaidUser_CanAlwaysPost()
    {
        using var scope = _factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDataSeeder.CreateUserAsync(scope.ServiceProvider, "paiduser@test.com", "Test1234!");

        // Assign paid subscription
        db.Subscriptions.Add(new Subscription
        {
            UserId    = user.Id,
            Plan      = SubscriptionPlan.Paid,
            Status    = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
        });

        // Add 10 posts
        for (int i = 0; i < 10; i++)
        {
            db.Posts.Add(new Post
            {
                AuthorUserId     = user.Id,
                Title            = $"Post {i}",
                Body             = "Test body content here",
                ModerationStatus = ModerationStatus.Approved,
                Status           = PostStatus.Active,
                CreatedAt        = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        Assert.True(await svc.CanPostAsync(user.Id));
    }
}
