using frontida4baby.Tests.Helpers;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using System.Net;
using Xunit;

namespace frontida4baby.Tests;

public class AuthTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetRegisterPage_Returns200()
    {
        var resp = await _client.GetAsync("/account/register");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetLoginPage_Returns200()
    {
        var resp = await _client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CreateUser_ViaUserManager_SucceedsAndUserExistsInDb()
    {
        using var scope = _factory.Services.CreateScope();
        var user = await TestDataSeeder.CreateUserAsync(
            scope.ServiceProvider, "created@test.com", "Test1234!");

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(db.Users.FirstOrDefault(u => u.Email == "created@test.com"));
    }

    [Fact]
    public async Task Login_ValidPassword_PasswordCheckSucceeds()
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.CreateUserAsync(scope.ServiceProvider, "signintest@test.com", "Test1234!");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("signintest@test.com");
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user, "Test1234!"));
    }

    [Fact]
    public async Task Login_WrongPassword_PasswordCheckFails()
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.CreateUserAsync(scope.ServiceProvider, "wrongpass@test.com", "Test1234!");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("wrongpass@test.com");
        Assert.NotNull(user);
        Assert.False(await userManager.CheckPasswordAsync(user, "WrongPassword!"));
    }

    [Fact]
    public async Task DashboardPage_UnauthenticatedUser_Redirects()
    {
        var resp = await _client.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    [Fact]
    public async Task SubscriptionPage_Returns200()
    {
        var resp = await _client.GetAsync("/subscription");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DisclaimerPage_Returns200()
    {
        var resp = await _client.GetAsync("/disclaimer");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
