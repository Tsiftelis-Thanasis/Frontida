# frontida4baby — Development Handoff

## Status: All 10 phases implemented, 16/16 tests passing

---

## Quick Start on New Machine

```bash
# 1. Clone & open solution
git clone <repo-url>
cd Frontida
# Open frontida4baby.slnx in Visual Studio / Rider

# 2. Restore packages
dotnet restore frontida4baby.Web/frontida4baby.Web.csproj
dotnet restore frontida4baby.Tests/frontida4baby.Tests/frontida4baby.Tests.csproj

# 3. Apply DB migrations (SQL Server LocalDB, auto-runs on startup too)
cd frontida4baby.Web
~/.dotnet/tools/dotnet-ef database update

# 4. Run app
dotnet run

# 5. Run tests
cd ../frontida4baby.Tests/frontida4baby.Tests
dotnet test
```

**Test result:** 16/16 passing (PostTests: 4, ReactionTests: 4, AuthTests: 8)

---

## Solution Structure

```
Frontida/
├── frontida4baby.Web/          ← main ASP.NET Core MVC app (.NET 10)
├── frontida4baby.Tests/
│   └── frontida4baby.Tests/    ← xUnit integration tests
└── frontida4baby.slnx          ← solution file
```

### Web project layout

```
frontida4baby.Web/
├── Controllers/
│   ├── AccountController.cs        auth (register/login/logout/OAuth)
│   ├── AdminController.cs          /admin/** [Authorize(Roles="Admin")]
│   ├── CaregiversController.cs     /caregivers browse
│   ├── DashboardController.cs      /dashboard/** [Authorize]
│   ├── HomeController.cs           / , /disclaimer
│   ├── LanguageController.cs       /language/set (culture cookie)
│   ├── ModerationController.cs     /moderation/queue
│   ├── PostsController.cs          /posts CRUD + replies + edit
│   ├── ReactionsController.cs      /reactions/toggle/{postId} [Authorize]
│   ├── SavedPostsController.cs     /saved/toggle/{postId} [Authorize]
│   └── SubscriptionController.cs   /subscription + Stripe webhook
├── Data/
│   └── ApplicationDbContext.cs
├── Middleware/
│   └── ErrorLoggingMiddleware.cs   catches unhandled exceptions → AppLog + email
├── Migrations/
│   ├── 20260124205654_InitialCreate
│   ├── 20260302235415_AddPostsRepliesModeration
│   └── 20260303211232_AddReactionsSavesSubscriptionLogs  ← latest
├── Models/
│   ├── Entities/
│   │   ├── AppLog.cs
│   │   ├── ApplicationUser.cs
│   │   ├── ModerationEnums.cs      (also SubscriptionPlan, AppLogLevel enums)
│   │   ├── Post.cs
│   │   ├── PostReaction.cs
│   │   ├── Reply.cs
│   │   ├── SavedPost.cs
│   │   └── Subscription.cs
│   └── ViewModels/
│       ├── DashboardViewModel.cs
│       ├── PostDetailViewModel.cs  (has ReactionCount, CurrentUserLiked, CanEdit …)
│       └── UserSettingsViewModel.cs
├── Services/
│   ├── AppLogService.cs / IAppLogService.cs
│   ├── ClaudeModerationService.cs
│   ├── ContentModerationService.cs / IContentModerationService.cs
│   ├── EmailOptions.cs
│   ├── IEmailService.cs / SmtpEmailService.cs / NoOpEmailService.cs
│   ├── ISubscriptionService.cs / SubscriptionService.cs
│   └── WordlistModerationService.cs
└── Views/
    ├── Admin/          Index, Users, UserDetail, Posts, Logs
    ├── Dashboard/      Index, Settings
    ├── Home/           Index, Disclaimer
    ├── Moderation/     Queue, Logs
    ├── Posts/          Index, Create, Details, Edit
    ├── Subscription/   Index, Success, Cancel
    └── Shared/         _Layout, _DisclaimerPartial
```

---

## Database

- **Connection string:** `(localdb)\MSSQLLocalDB;Database=frontida4babyDB`
- Migrations run automatically on startup (`db.Database.MigrateAsync()` — skipped for InMemory)
- **Admin role** seeded automatically on first run

### Entity summary

| Entity | Key relationships |
|--------|-------------------|
| ApplicationUser | → Subscription (1:1), → PostReactions, → SavedPosts |
| Post | → Reactions (PostReaction[]), → SavedBy (SavedPost[]) |
| PostReaction | Unique index: (PostId, UserId) |
| SavedPost | Unique index: (PostId, UserId) |
| Subscription | Plan: Free/Paid, Status: Active/Cancelled/Expired |
| AppLog | Level: Info/Warning/Error |

### Subscription limits

| | Free | Paid (€10/mo) |
|---|---|---|
| Posts/month | 5 | unlimited |
| Replies/month | 20 | unlimited |
| Reactions/month | 10 | unlimited |
| View phone numbers | No | Yes |

---

## External Services (configure in appsettings.json)

### Stripe (subscription payments)
```json
"Stripe": {
  "PublicKey": "",
  "SecretKey": "",
  "WebhookSecret": "",
  "PaidPriceId": ""
}
```
Webhook endpoint (antiforgery disabled): `POST /subscription/webhook`
Handles: `checkout.session.completed` → Plan=Paid, `customer.subscription.deleted` → Plan=Free

### Email (SMTP via MailKit)
```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUser": "",
  "SmtpPass": "",
  "FromAddress": "noreply@frontida4baby.gr",
  "FromName": "frontida4baby",
  "AdminEmail": ""
}
```
Short-circuits (no-op) if `SmtpUser` is empty. `NoOpEmailService` used in tests.

### Claude AI moderation
```json
"Moderation": {
  "ClaudeApiKey": "",
  "UseClaudeModeration": true
}
```
Model: `claude-haiku-4-5-20251001`

---

## Pending / Follow-up Work

These items from the original plan are **not yet done**:

1. **Email hook on registration** — `AccountController.Register [POST]` should fire-and-forget
   `_email.SendAsync(adminEmail, "New user: ...", ...)` after `_userManager.CreateAsync` succeeds.

2. **AppLog injection into controllers** — `PostsController` and `AccountController` should inject
   `IAppLogService` and call `LogAsync(AppLogLevel.Info, ...)` on login, register, logout, and
   post/reply creation events.

3. **Nav links** — `_Layout.cshtml` should show "Dashboard" and "Subscription" links for authenticated
   users in the navbar (currently admin dropdown exists but user-facing nav links may be missing).

4. **Database update on new machine** — After cloning, run:
   ```bash
   cd frontida4baby.Web
   ~/.dotnet/tools/dotnet-ef database update
   ```
   Or just `dotnet run` — migrations apply automatically on startup.

5. **Stripe live keys** — Fill in `appsettings.json` (or use user-secrets) before testing payments.

---

## Design System

- **Colors:** Primary `#2A9D8F` (teal), Dark `#264653`, Accent `#E76F51` (coral)
- **Font:** Nunito (Google Fonts CDN)
- **Icons:** Font Awesome 6 (CDN)
- **CSS classes:** `.hero`, `.service-card`, `.caregiver-card`, `.auth-wrapper/.auth-card`,
  `.page-header`, `.filter-card`, `.cta-card`
- All non-homepage views wrap content in `<div class="container py-4">`

## Localization

- Default culture: **el** (Greek)
- Resource files: `Resources/SharedResource.resx` (English fallback) + `Resources/SharedResource.el.resx`
- Injected as `L` in all views via `_ViewImports.cshtml`
- Toggle via `POST /language/set?culture=en&returnUrl=/`
