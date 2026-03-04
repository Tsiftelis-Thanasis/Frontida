# frontida4baby — Development Handoff

## Current state: all phases complete, 16/16 tests passing — no pending items

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

# 3. Run (migrations applied automatically on startup)
cd frontida4baby.Web
dotnet run

# 4. Run tests
cd ../frontida4baby.Tests/frontida4baby.Tests
dotnet test
# Expected: 16/16 passing (AuthTests: 8, PostTests: 4, ReactionTests: 4)
```

---

## What Is Implemented

### Phase 1 — Data Layer ✅
New entities: PostReaction, SavedPost, Subscription, AppLog.
Updated entities: ApplicationUser (nav props), Post (EditedAt, Reactions, SavedBy), Reply (EditedAt).
New enums: SubscriptionPlan, SubscriptionStatus, AppLogLevel.
Latest migration: `20260303211232_AddReactionsSavesSubscriptionLogs`

### Phase 2 — Subscription System ✅
`SubscriptionController`: GET /subscription (pricing page), POST /subscription/checkout (Stripe),
GET /subscription/success, GET /subscription/cancel, POST /subscription/webhook (antiforgery disabled).
`SubscriptionService` / `ISubscriptionService`: CanPostAsync, CanReplyAsync, CanReactAsync, IsPhoneVisibleAsync.
Views: Subscription/Index.cshtml (pricing cards Free vs €10/mo), Success.cshtml, Cancel.cshtml.

### Phase 3 — Reactions & Saves ✅
`ReactionsController`: POST /reactions/toggle/{postId} [Authorize] → JSON {liked, count}.
`SavedPostsController`: POST /saved/toggle/{postId} [Authorize] → JSON {saved}.
Posts/Details.cshtml updated with AJAX heart/bookmark buttons, reaction count, CanEdit flags.

### Phase 4 — Edit Functionality ✅
PostsController: GET/POST /posts/edit/{id} [Authorize, author-only].
PostsController: POST /posts/reply-edit/{replyId} [Authorize, author-only].
Edits re-run through ContentModerationService. EditedAt set on save.
Views/Posts/Edit.cshtml created.

### Phase 5 — User Dashboard ✅ (nav link missing — see Pending)
`DashboardController` [Authorize]:
- GET /dashboard — tab view: Posts | Replies | Reactions | Saved
- GET/POST /dashboard/settings — edit FirstName, LastName, Phone, City, Bio

Views: Dashboard/Index.cshtml (tabbed, subscription plan badge, free→paid upsell),
Dashboard/Settings.cshtml (profile form).
ViewModels: DashboardViewModel, UserSettingsViewModel, ReplyDashboardItem.

### Phase 6 — Admin Dashboard ✅ (admin user not seeded — see Pending)
`AdminController` [Authorize(Roles="Admin")]:
- GET /admin — stats: TotalUsers, PostsToday, PendingModeration, Errors24h
- GET /admin/users — paginated user list (20/page) with plan badge
- GET /admin/users/{id} — user detail: info, subscription, last 10 posts
- POST /admin/users/{id}/set-plan — assign Free or Paid plan
- GET /admin/posts — all posts with moderation status filter
- GET /admin/logs — AppLog viewer with level + date range filters

Views: Admin/Index.cshtml, Users.cshtml, UserDetail.cshtml, Posts.cshtml, Logs.cshtml.
_Layout.cshtml: Admin dropdown (shield icon, red) visible to Admin role users only.

### Phase 7 — Email Notifications ✅ (registration hook missing — see Pending)
MailKit/MimeKit. Services: IEmailService, SmtpEmailService, NoOpEmailService (used in tests).
Short-circuits if SmtpUser config is empty. ErrorLoggingMiddleware sends email to AdminEmail on Error.

### Phase 8 — Application Logging ✅ (controller-level logging missing — see Pending)
IAppLogService / AppLogService — writes AppLog to DB.
ErrorLoggingMiddleware — catches unhandled exceptions → AppLog(Error) + admin email → re-throws.

### Phase 9 — Disclaimer ✅
Views/Shared/_DisclaimerPartial.cshtml — fixed bottom banner, dismissible via localStorage.
Views/Home/Disclaimer.cshtml — full page.
HomeController.Disclaimer() with [Route("disclaimer")] attribute.
_Layout.cshtml includes partial before </body>.

### Phase 10 — Integration Tests ✅
16/16 tests passing. InMemory DB, NoOpEmailService stub.
TestWebApplicationFactory removes IDbContextOptionsConfiguration descriptors to prevent duplicate
provider error. Program.cs uses db.Database.IsRelational() check before MigrateAsync.

---

## Pending Work

None — all items resolved.

## First-Run Admin Credentials

`admin@frontida4baby.gr` / `Admin1234!` (seeded automatically on startup)
Change the password after first login.

---

## Architecture Reference

### Solution Structure
```
Frontida/
├── frontida4baby.Web/          .NET 10, ASP.NET Core MVC
├── frontida4baby.Tests/
│   └── frontida4baby.Tests/    xUnit, WebApplicationFactory, InMemory EF
└── frontida4baby.slnx
```

### Controllers
| Controller | Route | Auth |
|------------|-------|------|
| HomeController | /, /disclaimer | public |
| AccountController | /account/* | public / [Authorize] |
| CaregiversController | /caregivers | public |
| PostsController | /posts | public / [Authorize] |
| ReactionsController | /reactions/toggle/{id} | [Authorize] |
| SavedPostsController | /saved/toggle/{id} | [Authorize] |
| DashboardController | /dashboard | [Authorize] |
| SubscriptionController | /subscription | public / [Authorize] |
| ModerationController | /moderation | [Authorize(Roles="Admin")] |
| AdminController | /admin | [Authorize(Roles="Admin")] |
| LanguageController | /language/set | public |

### Database
- Connection: `(localdb)\MSSQLLocalDB;Database=frontida4babyDB`
- Migrations auto-apply on startup
- Admin role seeded on startup
- **Admin user NOT seeded yet** (see Pending #1)

### Subscription Limits
| | Free | Paid (€10/mo) |
|---|---|---|
| Posts/month | 5 | unlimited |
| Replies/month | 20 | unlimited |
| Reactions/month | 10 | unlimited |
| View phone numbers | No | Yes |

### External Config (appsettings.json)
```json
"Stripe": {
  "PublicKey": "",
  "SecretKey": "",
  "WebhookSecret": "",
  "PaidPriceId": ""
},
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
Both Stripe and Email short-circuit silently if keys are empty.

### Design System
- Colors: Primary `#2A9D8F`, Dark `#264653`, Accent `#E76F51`
- Font: Nunito (Google Fonts CDN) + Font Awesome 6 (CDN)
- Non-homepage views: `<div class="container py-4">`
- Localization: `el` default, `en` supported; strings in `Resources/SharedResource.*.resx`
