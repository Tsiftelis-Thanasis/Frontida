# Changelog

All notable changes to frontida4baby, developed with Claude AI assistance.

---

## [Unreleased] — 2026-03-26

### Changed
- Redesigned home page (`Home/Index.cshtml`) — cleaner layout, new hero section
- Added 4 home page design templates (`Template1–4.cshtml`) as design alternatives
- Introduced `stitch-tokens.css` and `stitch-components.css` design system files
- Refactored `site.css` — significant cleanup and restructure
- Updated cookie consent partial (`_CookieConsentPartial.cshtml`) — UI improvements
- Updated disclaimer partial (`_DisclaimerPartial.cshtml`)
- Improved layout (`_Layout.cshtml`) — minor fixes and refinements
- Improved Posts list view (`Posts/Index.cshtml`) — display fixes
- Improved Post details view (`Posts/Details.cshtml`) — UI enhancements
- Updated login and register views — minor fixes
- Updated admin Posts view (`Admin/Posts.cshtml`) — improved layout
- Updated dashboard (`Dashboard/Index.cshtml`, `Dashboard/Settings.cshtml`)
- Updated caregiver listings (`Caregivers/Index.cshtml`)
- Updated `WordlistModerationService` — rule refinements
- Added new localization keys to `SharedResource.el.resx` / `SharedResource.resx`
- Updated `PostListViewModel`, `PostDetailViewModel`, `UserSettingsViewModel`, `DashboardViewModel`
- Various controller fixes: `DashboardController`, `HomeController`, `ModerationController`, `PostsController`, `ReactionsController`

---

## [Safety & Legal] — 2026-03-14

### Fixed
- Email template links in legal pages (`Privacy`, `Disclaimer`, `AiReviewPolicy`, `TermsOfService`)
- `appsettings.json` — corrected email/hook configuration values

---

## [Payments & Notifications] — 2026-03-12

### Added
- **Payments dashboard** (`Admin/Payments.cshtml`) — admin view of all Stripe transactions
- **Support page** (`Home/Support.cshtml`) — user-facing contact/support form
- `IUserEmailService` / `UserEmailService` — dedicated service for transactional user emails
- `PaymentViewModel`, `SupportViewModel`
- `NotifyPaymentReceivedAsync` notification method
- Admin index link to payments dashboard

### Fixed
- Email notification delivery (SMTP configuration)
- Post reaction approval logic (`ReactionsController`)
- `AdminController` — UserDetail fixes and payment data loading
- `SubscriptionController` — post-payment logic (success/webhook handling)
- `HomeController` — support form submission handler

---

## [Safety Features & Legal] — 2026-03-10

### Added
- **Blacklist system**: `IsBlacklisted`, `BlacklistReason`, `BlacklistedAt` on `ApplicationUser`; auto-blacklist after 3 content rejections (configurable via `ModerationOptions.BlacklistThreshold`)
- **Terms acceptance**: `HasAcceptedTerms` / `TermsAcceptedAt` on `ApplicationUser`; required checkbox on registration form
- **Email verification flow**: Non-blocking background send on register; confirm via token link; resend option; dashboard unverified banner; profile badge (`✓ Επιβεβαιωμένος`)
- **Legal pages**: `/Privacy` (full GDPR policy), `/terms` (Terms of Service), `/disclaimer`, `/ai-review-policy`
- **Cookie consent banner** (`_CookieConsentPartial.cshtml`) — GDPR compliant
- **Notification service** (`INotificationService` / `NotificationService`): typed admin alerts — `NotifyNewRegistrationAsync`, `NotifyServerErrorAsync`, `NotifyUserBlacklistedAsync`, `NotifyPostRejectedAsync`
- `NotificationOptions` bound from `appsettings.json` for per-event toggles
- **Caregiver details page** (`Caregivers/Details.cshtml`)
- **Reaction approval UI** on Post Details — OP sees panel with Approve buttons; approved caregivers see contact details; others see "waiting" notice
- Email verification views: `VerifyEmailSent.cshtml`, `ConfirmEmailResult.cshtml`
- Admin: unblacklist action in `UserDetail`
- Dashboard: email verification banner
- Footer links to all legal pages
- DB migration: `LegalAndSafetyFeatures`
- favicon (`wwwroot/favicon.svg`)

### Changed
- `ContentModerationService` now fires blacklist and rejection notifications (fire-and-forget)
- `AccountController` fires new-registration notification after sign-up
- `ErrorLoggingMiddleware` refactored to use `INotificationService`
- `PostsController` and `ReactionsController` guard against blacklisted users

---

## [Core Platform Rebuild] — 2026-03-03 / 2026-03-04

### Added
- **Community Posts system**: `Post`, `Reply`, `PostReaction`, `SavedPost` entities; full CRUD with `PostsController`
- **Moderation pipeline** (2-stage): `WordlistModerationService` (fast pre-filter) → `ClaudeModerationService` (AI review via Claude API)
- `ContentModerationService` — orchestrates both stages, logs decisions, triggers auto-blacklist
- `ModerationLog` entity — audit trail for all moderation decisions
- `PendingModerationJob` — hosted service that processes the moderation queue in background
- **Subscription system**: `Subscription` entity; `ISubscriptionService` / `SubscriptionService`; gating on post/reply/react actions; `SubscriptionController` with Stripe checkout
- **Admin dashboard**: user list, user detail, moderation queue, moderation logs, app logs
- `ModerationController` — admin moderation queue management
- `DashboardController` — user dashboard with post history and stats
- `AppLog` / `AppLogService` — structured application logging
- `ErrorLoggingMiddleware` — catches unhandled exceptions, logs to DB
- `ProfileController` and public profile view
- `DevDataSeeder` — seed data for development
- **Localization**: `SharedResource.el.resx` (Greek) and `SharedResource.resx` (English) with full key sets
- `LanguageController` — language switcher
- **OAuth support**: `ExternalLoginConfirmation` flow; OAuth users skip email verification
- `UserSettingsViewModel` — user account settings
- Star rating partial (`_StarRating.cshtml`)
- Subscription views (Index, Success, Cancel)
- Dockerfile and `.dockerignore`
- Test project (`frontida4baby.Tests`) with auth, post, and reaction integration tests

### Changed
- Project renamed/restructured from `Frontida.Web` to `frontida4baby.Web`
- `ApplicationDbContext` fully rebuilt with all new entities
- `Program.cs` wired with all services, middleware, localization, and Stripe
- DB migrations: `AddPostsRepliesModeration`, `AddReactionsSavesSubscriptionLogs`

---

## [Initial Setup] — 2026-01-24

### Added
- Initial .NET 10 MVC project scaffold
- ASP.NET Core Identity with `ApplicationUser`
- `ApplicationDbContext` with base entities (Profile, Service, Booking, Review, Message)
- Basic caregiver search and listings (`CaregiversController`)
- Bootstrap 5 layout
- Azure App Service deployment workflow (GitHub Actions)
- DB migration: `InitialCreate`
