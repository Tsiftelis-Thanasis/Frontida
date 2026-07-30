using System.Text;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using frontida4baby.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(rawConnectionString))
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing or empty. " +
        "Check the ConnectionStrings__DefaultConnection environment variable — " +
        "if it uses a ${{ }} reference (e.g. Railway variable references), confirm it actually " +
        "resolved to a real value rather than an empty string.");

// Managed Postgres hosts (Railway, Render, Heroku, etc.) commonly hand out
// postgres://user:pass@host:port/db URIs rather than Npgsql's native
// Host=...;Username=...;Password=... key-value format. Accept either.
var connectionString = NpgsqlConnectionStringFromUri(rawConnectionString);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

static string NpgsqlConnectionStringFromUri(string value)
{
    if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return value;

    var uri = new Uri(value);
    var userInfo = uri.UserInfo.Split(':', 2);
    var database = uri.AbsolutePath.TrimStart('/');

    return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={database};" +
           $"Username={Uri.UnescapeDataString(userInfo[0])};" +
           $"Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : "")};" +
           "SSL Mode=Require;Trust Server Certificate=true";
}

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
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

var moderationConfig = builder.Configuration.GetSection("Moderation").Get<ModerationOptions>()
    ?? new ModerationOptions();
var moderationAttemptTimeout = TimeSpan.FromSeconds(moderationConfig.TimeoutSeconds);

builder.Services.AddSingleton<WordlistModerationService>();
builder.Services.AddHttpClient<ClaudeModerationService>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.UseJitter = true;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = moderationAttemptTimeout;
        options.TotalRequestTimeout.Timeout = moderationAttemptTimeout * 3;
    });
builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
builder.Services.AddScoped<ContentModerationService>(); // needed for LogAsync

builder.Services.AddHostedService<PendingModerationJob>();

// ── Subscription service ──────────────────────────────────────────────────────
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// ── Invoicing (myDATA data-readiness) ─────────────────────────────────────────
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection("Company"));
builder.Services.AddScoped<IInvoicingService, LocalInvoicingService>();

// ── Email service ─────────────────────────────────────────────────────────────
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// ── Notification service ──────────────────────────────────────────────────────
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddScoped<INotificationService, NotificationService>();

// ── User email service ────────────────────────────────────────────────────────
builder.Services.Configure<UserEmailOptions>(builder.Configuration.GetSection("UserEmails"));
builder.Services.AddScoped<IUserEmailService, UserEmailService>();

// ── Application log service ───────────────────────────────────────────────────
builder.Services.AddScoped<IAppLogService, AppLogService>();

// ── Health checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

// ── Localisation ──────────────────────────────────────────────────────────────
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(5);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("webhook", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("content-create", o =>
    {
        o.PermitLimit = 20;
        o.Window = TimeSpan.FromHours(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("support", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(10);
        o.QueueLimit = 0;
    });
});

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
    var adminEmail    = builder.Configuration["Admin:Email"];
    var adminPassword = builder.Configuration["Admin:Password"];

    if (app.Environment.IsProduction() && (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)))
    {
        // In Production, admin credentials MUST be set via environment variables or User Secrets.
        // Do not auto-seed with defaults.
    }
    else
    {
        // In Development, fall back to defaults if not configured
        adminEmail    ??= "admin@frontida4baby.gr";
        adminPassword ??= "Admin1234!";

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

// ── Security headers ─────────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

// ── Rate limiting ────────────────────────────────────────────────────────────
app.UseRateLimiter();

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

app.MapHealthChecks("/health");

app.Run();
