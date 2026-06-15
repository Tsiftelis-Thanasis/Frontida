using frontida4baby.Web.Data;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace frontida4baby.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext registrations for ApplicationDbContext
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().FullName == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1" &&
                     d.ServiceType.GenericTypeArguments.Length == 1 &&
                     d.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext)))
                .ToList();

            foreach (var d in descriptorsToRemove)
                services.Remove(d);

            // Add InMemory database — capture name once so all scopes share the same DB
            var dbName = "TestDb_" + Guid.NewGuid();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // Stub out email
            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddSingleton<IEmailService, NoOpEmailService>();
        });

        builder.UseEnvironment("Development");
    }
}
