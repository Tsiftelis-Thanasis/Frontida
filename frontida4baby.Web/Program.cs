using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using frontida4baby.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ── Social / OAuth providers ──────────────────────────────────────────────────
var auth = builder.Services.AddAuthentication();
var cfg  = builder.Configuration;

if (!string.IsNullOrWhiteSpace(cfg["Authentication:Google:ClientId"]))
    auth.AddGoogle(o =>
    {
        o.ClientId     = cfg["Authentication:Google:ClientId"]!;
        o.ClientSecret = cfg["Authentication:Google:ClientSecret"]!;
    });

if (!string.IsNullOrWhiteSpace(cfg["Authentication:Facebook:AppId"]))
    auth.AddFacebook(o =>
    {
        o.AppId     = cfg["Authentication:Facebook:AppId"]!;
        o.AppSecret = cfg["Authentication:Facebook:AppSecret"]!;
    });

if (!string.IsNullOrWhiteSpace(cfg["Authentication:Microsoft:ClientId"]))
    auth.AddMicrosoftAccount(o =>
    {
        o.ClientId     = cfg["Authentication:Microsoft:ClientId"]!;
        o.ClientSecret = cfg["Authentication:Microsoft:ClientSecret"]!;
    });

if (!string.IsNullOrWhiteSpace(cfg["Authentication:Apple:ServicesId"]))
    auth.AddApple(o =>
    {
        o.ClientId             = cfg["Authentication:Apple:ServicesId"]!;
        o.TeamId               = cfg["Authentication:Apple:TeamId"]!;
        o.KeyId                = cfg["Authentication:Apple:KeyId"]!;
        o.GenerateClientSecret = true;
        o.PrivateKey           = (_, _) => Task.FromResult(
            cfg["Authentication:Apple:PrivateKey"]!.AsMemory());
    });

// ── Moderation services ───────────────────────────────────────────────────────
builder.Services.Configure<ModerationOptions>(
    builder.Configuration.GetSection("Moderation"));

builder.Services.AddSingleton<WordlistModerationService>();
builder.Services.AddHttpClient<ClaudeModerationService>();
builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
builder.Services.AddScoped<ContentModerationService>(); // needed for LogAsync

builder.Services.AddHostedService<PendingModerationJob>();

// ── Subscription service ──────────────────────────────────────────────────────
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// ── Email service ─────────────────────────────────────────────────────────────
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// ── Notification service ──────────────────────────────────────────────────────
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddScoped<INotificationService, NotificationService>();

// ── Application log service ───────────────────────────────────────────────────
builder.Services.AddScoped<IAppLogService, AppLogService>();

// ── Localisation ──────────────────────────────────────────────────────────────
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var app = builder.Build();

// ── Migrate database & seed Admin role ───────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string adminEmail    = "admin@frontida4baby.gr";
    const string adminPassword = "Admin1234!";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var adminUser = new ApplicationUser
        {
            UserName       = adminEmail,
            Email          = adminEmail,
            EmailConfirmed = true,
            FirstName      = "Admin",
            LastName       = "Admin",
        };
        var created = await userManager.CreateAsync(adminUser, adminPassword);
        if (created.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // ── Dev seed data (demo users, posts, replies) ────────────────────────
    if (app.Environment.IsDevelopment())
    {
        var seederLogger = scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();
        await DevDataSeeder.SeedAsync(scope.ServiceProvider, seederLogger);
    }
}

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ── Error logging middleware ──────────────────────────────────────────────────
app.UseMiddleware<ErrorLoggingMiddleware>();

// ── Localisation middleware ────────────────────────────────────────────────────
var supportedCultures = new[] { "el", "en" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("el")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
