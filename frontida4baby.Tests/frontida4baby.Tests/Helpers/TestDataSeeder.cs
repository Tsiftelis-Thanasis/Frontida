using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace frontida4baby.Tests.Helpers;

public static class TestDataSeeder
{
    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string email = "test@example.com",
        string password = "Test1234!",
        bool isCaregiver = false)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName    = email,
            Email       = email,
            FirstName   = "Test",
            LastName    = "User",
            IsCaregiver = isCaregiver,
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new Exception($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        return user;
    }

    public static async Task EnsureAdminRoleAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}
