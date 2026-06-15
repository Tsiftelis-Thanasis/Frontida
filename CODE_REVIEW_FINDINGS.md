# frontida4baby — Code Review Findings & Implementation Plan

**Reviewed:** `C:\Users\ttsiftelis_scytales\Downloads\Frontida\Frontida`
**Date:** 2026-06-10
**Scope:** Code flaws, UI/UX, flow gaps, logic/account correctness, AI moderation service availability (24/7).
**Solution:** ASP.NET Core MVC (.NET 10), EF Core / SQL Server, ASP.NET Identity, Stripe subscriptions, Claude (Haiku) content moderation.

---

## Executive Summary

The codebase is well-structured for a solo project: clean two-stage moderation pipeline (wordlist → Claude), a background re-processing job, a manual moderation queue, Stripe webhook handling with signature verification, and a test suite that covers the webhook and subscription-limit logic well. The big wins are in four areas:

1. **Security hardening** — a hardcoded production admin password, no security headers, no rate limiting, no login lockout, and weak password rules.
2. **Account flows** — email confirmation is not actually enforced, there is no password-reset flow, and external-login account linking trusts the provider email.
3. **AI service resilience (the "24/7" question)** — no retries, no circuit breaker, no health check, and a default fallback that silently stalls the whole content pipeline during a Claude outage. There is also no per-item retry cap, so genuinely-ambiguous content is re-sent to the API forever.
4. **A logic bug that defeats auto-blacklisting** — hard rejections at creation time are never logged, so the rejection counter never increments.

Nothing here is unfixable; most items are small, targeted changes. The plan at the bottom sequences them.

### Severity legend
- **Critical** — security/data-loss/abuse risk; fix before any public deployment.
- **High** — broken or unsafe behavior users/admins will hit; fix soon.
- **Medium** — correctness/performance/maintainability; schedule it.
- **Low** — polish, consistency, minor risk.

### Finding count by area

| Area | Critical | High | Medium | Low |
|---|---|---|---|---|
| Security & configuration | 1 | 2 | 4 | 3 |
| Account & auth flows | 0 | 3 | 2 | 1 |
| AI moderation service (24/7) | 0 | 3 | 3 | 1 |
| Payments / subscriptions | 0 | 1 | 2 | 1 |
| Logic & flow | 0 | 2 | 3 | 1 |
| Data layer / EF Core | 0 | 4 | 5 | 3 |
| UI / UX | 0 | 1 | 4 | 6 |
| Localization | 0 | 2 | 2 | 1 |
| Testing | 0 | 1 | 2 | 0 |

---

## 1. Security & Configuration

### SEC-1 — Hardcoded admin account seeded in every environment *(Critical)*
`frontida4baby.Web/Program.cs:120-135`
The bootstrap creates `admin@frontida4baby.gr` with the literal password `Admin1234!` and runs in **all** environments, including Production. Anyone who reads the source (or guesses) owns the admin role.
**Fix:** Move `adminEmail`/`adminPassword` to User Secrets / environment variables. In Production, fail fast if unset and never use a default. Force a password change on first login. Only auto-seed in Development.

### SEC-2 — No login lockout (brute-force open) *(High)*
`frontida4baby.Web/Controllers/AccountController.cs:122-123`
`PasswordSignInAsync(..., lockoutOnFailure: false)` disables Identity's lockout entirely. Combined with no rate limiting (SEC-4), credential stuffing is unthrottled.
**Fix:** Set `lockoutOnFailure: true`, configure `options.Lockout` in `Program.cs` (e.g. 5 attempts / 15-min lockout), and ensure `IdentityOptions.Lockout.AllowedForNewUsers = true`.

### SEC-3 — No security response headers *(High)*
`frontida4baby.Web/Program.cs` (pipeline ~147-170)
No Content-Security-Policy, `X-Content-Type-Options`, `X-Frame-Options`/frame-ancestors, or `Referrer-Policy`. The app renders user-generated content and builds HTML emails, so headers matter.
**Fix:** Add a security-headers middleware (or the `NetEscapades.AspNetCore.SecurityHeaders` package). Start with `X-Content-Type-Options: nosniff`, frame-ancestors `'none'`, a restrictive `Referrer-Policy`, and a CSP scoped to your CDN/Unsplash usage.

### SEC-4 — No rate limiting *(Medium)*
`frontida4baby.Web/Program.cs`
Login, register, the support form, and `/subscription/webhook` are unthrottled. Each unhandled error also writes a DB row **and** emails the admin (see LOG/DATA-11), so a repeated 500 becomes an email flood.
**Fix:** `builder.Services.AddRateLimiter(...)` + `app.UseRateLimiter()`; apply a stricter policy to auth and webhook endpoints.

### SEC-5 — Weak password / account policy *(Medium)*
`frontida4baby.Web/Program.cs:21-26`
`RequiredLength = 6`, `RequireNonAlphanumeric = false`, `RequireConfirmedAccount = false`. For a platform holding family/caregiver PII this is weak.
**Fix:** Raise minimum length to 8–10; reconsider requiring confirmed accounts (see ACC-1).

### SEC-6 — `AllowedHosts: "*"` *(Low)*
`frontida4baby.Web/appsettings.json:43`
Wildcard host binding permits Host-header spoofing.
**Fix:** Set the explicit production hostname.

### SEC-7 — `.claude/settings.local.json` tracked in source *(Low)*
`Frontida/.claude/settings.local.json`
`*.local.json` is conventionally git-ignored; this permission allow-list is committed. No secret content, but the pattern is wrong.
**Fix:** Add `**/.claude/settings.local.json` to `.gitignore`.

### SEC-8 — `appsettings*.json` are tracked, not ignored *(Low)*
`frontida4baby.Web/appsettings.json`, `appsettings.Development.json`
**Currently safe** — verified no live secrets are committed (Claude key, Stripe keys, connection string, and SMTP password are all absent/empty and read from User Secrets at runtime). The risk is future: a secret typed into these files would be committed.
**Fix:** Keep using User Secrets; add a pre-commit secret scan. Do **not** blanket-ignore `appsettings.json` (it carries needed non-secret config).

> **Positive:** Secret handling is otherwise sound — the Stripe webhook validates the HMAC signature before acting (`SubscriptionController.cs:147-151`), and all email builders HTML-encode user input. No live secrets found in source.

---

## 2. Account & Auth Flows

### ACC-1 — Email confirmation is not enforced (the verify flow is cosmetic) *(High)*
`Program.cs:21` (`RequireConfirmedAccount = false`) + `AccountController.cs:64-91`
After `CreateAsync`, the user is immediately signed in (`SignInAsync`) and redirected to "check your inbox". They can use the entire app without ever confirming. The `VerifyEmailSent` / `ConfirmEmail` / `ResendVerification` machinery has no teeth.
**Fix:** Decide the intended behavior. Either (a) set `RequireConfirmedAccount = true` and don't sign in until confirmed, or (b) keep instant access but gate sensitive actions (posting, reacting, contact reveal) behind `EmailConfirmed`. Today it's neither.

### ACC-2 — No password-reset / forgot-password flow *(High)*
`AccountController.cs` (entire file)
There are no `ForgotPassword` / `ResetPassword` actions or views. A user who forgets their password is permanently locked out (unless they used social login).
**Fix:** Add the standard Identity forgot/reset-password actions + views + email template (mirrors the existing `SendVerificationEmailAsync` pattern).

### ACC-3 — External login auto-links by email without proof of ownership *(High)*
`AccountController.cs:166-178`
On external login, if an account with the same email exists, the external login is linked to it automatically. If a provider ever returns an unverified email, this is an account-takeover path. Google/Microsoft/Apple verify emails, but the code does not check the `email_verified` claim.
**Fix:** Only auto-link when the provider asserts a verified email; otherwise require the user to log in with their password first to confirm ownership before linking.

### ACC-4 — Email confirmation token double-encoded *(Medium)*
`AccountController.cs:72-75` and `280-281`
The token is `WebUtility.UrlEncode`-d, then passed to `Url.Action` which URL-encodes it **again**; on confirm it's `UrlDecode`-d once. This relies on model binding decoding the other layer and is fragile (the classic `+`-becomes-space bug). It works today but breaks unpredictably.
**Fix:** Don't pre-encode — let `Url.Action` handle encoding and read the raw bound value, or Base64Url-encode the token bytes explicitly on both ends.

### ACC-5 — Brand-name mismatch in email/UI: "frontida4all" vs "frontida4baby" *(Medium)*
`AccountController.cs:336` (`?? "frontida4all"`), `Views/Shared/_Layout.cshtml:8,29,158,179`, legal pages
The product is **frontida4baby** but the layout title, navbar brand, footer, verification email default, and legal text say **frontida4all**. Confusing and looks unfinished.
**Fix:** Pick one brand string, centralize it (config or a resource key), and replace all occurrences.

### ACC-6 — Registration/verification/admin-notify run as fire-and-forget `Task.Run` *(Low)*
`AccountController.cs:78-89, 288-292, 323-327`
These detach work onto the thread pool capturing services; exceptions are swallowed and the work isn't tracked on shutdown. For email it's tolerable ("best-effort"), but it's the same anti-pattern that is genuinely dangerous elsewhere (see AI-3 / DATA-2).
**Fix:** Prefer awaiting these (email sends are fast) or use a proper background queue (`Channel<T>` + hosted service) so failures are observable.

---

## 3. AI Moderation Service — Availability & Correctness ("is it 24/7?")

This is the area you specifically asked about. The design is reasonable (instant wordlist stage, Claude semantic stage, background re-processing, manual fallback queue), but it is **not** resilient to a sustained Claude outage and has no monitoring.

### AI-1 — A Claude outage silently stalls the entire content pipeline *(High)*
`ClaudeModerationService.cs:120-129` + `ModerationOptions.cs:10` (`FallbackOnTimeout = "PendingReview"`)
On any failure (timeout, 5xx, parse error) the service returns the configured fallback — by default **PendingReview**. So during a Claude outage, every new post/reply that passes the wordlist becomes invisible to everyone and piles into the manual queue. There is no operator signal that this is happening.
**Fix:** (a) Add alerting when the fallback path fires repeatedly (e.g. notify admin after N consecutive failures). (b) Decide an explicit "degraded mode" policy. (c) Make sure admins are told the queue is filling (the `Admin/Index` pending counter helps but isn't push).

### AI-2 — `PendingModerationJob` re-sends genuinely-ambiguous items forever (no retry cap) *(High)*
`Services/PendingModerationJob.cs:54-64, 74-131`
Every 5 minutes the job re-queries **all** `PendingReview` posts/replies and re-calls Claude. Items Claude legitimately keeps marking `PendingReview` are re-sent indefinitely — wasted API spend and unbounded run time. There is also no cap on items per run; with thousands pending and a 10 s timeout each, a single run can take hours and block the next.
**Fix:** Add an attempt counter (or `LastModerationAttemptAt` + max-retries) to `Post`/`Reply`; after N attempts, stop auto-retrying and leave it for the manual queue. Cap items processed per run (e.g. take 50, oldest first). Add backoff when the API is failing.

### AI-3 — Background job's fire-and-forget notifications use a disposed scope *(High)*
`PendingModerationJob.cs:36-37` (`using var scope`) → `ContentModerationService.LogAsync:86-109, 130-138`
`LogAsync` spawns `Task.Run(...)` that uses the scoped `_notifications` / `_userEmail` (which internally use the scoped `DbContext`). The job's `using var scope` is disposed as soon as `ProcessPendingAsync` returns, so those detached tasks can hit `ObjectDisposedException` on the `DbContext`. Failures are swallowed, so rejection emails silently vanish.
**Fix:** In the fire-and-forget tasks, create a fresh scope via `IServiceScopeFactory` and pass primitive values (not tracked entities), or replace fire-and-forget with an awaited background queue.

### AI-4 — No retry / circuit breaker on the Claude HTTP client *(Medium)*
`Program.cs:72` + `ClaudeModerationService.cs:80-118`
`AddHttpClient<ClaudeModerationService>()` has no resilience handler. A single transient blip (one dropped connection, one 529) immediately falls back to PendingReview.
**Fix:** Add `Microsoft.Extensions.Http.Resilience` (or Polly) — retry with exponential backoff + jitter on transient errors, plus a circuit breaker so you stop hammering a down API. Distinguish 429/529 (retry) from 400/401 (don't retry — config error).

### AI-5 — No health check / uptime monitoring *(Medium)*
`Program.cs` (no `AddHealthChecks`)
There is no `/health` endpoint, no health check for SQL Server or the Claude API, and nothing for an uptime monitor / load balancer to probe. You can't currently answer "is it up?" automatically.
**Fix:** `AddHealthChecks()` with a DB check and a lightweight Claude reachability check; expose `/health` (liveness) and `/health/ready` (readiness). Wire an external uptime monitor (Azure App Service health check, UptimeRobot, etc.) and Sentry/Serilog alerts.

### AI-6 — Claude reply parsed as raw JSON; markdown-wrapped output silently fails *(Medium)*
`ClaudeModerationService.cs:90-98`
If Claude ever returns the JSON wrapped in a markdown fence (despite the prompt), `JsonSerializer.Deserialize<ModerationDecision>(text)` throws → fallback PendingReview. Brittle.
**Fix:** Strip ```` ```json ```` fences / extract the first `{...}` block before deserializing; log the raw text on parse failure so you can see what Claude actually returned.

### AI-7 — Leet-map applied to all text causes potential false positives; dead duplicate entry *(Low)*
`Services/WordlistModerationService.cs:157-174`, dup at `:94`
`Normalise` maps digits to letters across the **entire** text (`4→a`, `1→i`, `0→o`, `5→s`…), so legitimate content like rates/phone numbers ("15€", "24h") gets mangled before matching. Low real-world risk, but worth bounding. The Greek threat list also has a duplicate entry (`θα σε σκοτωσω` twice) — dead code.
**Fix:** Apply leet normalization only in the whole-word profanity pass, not the substring passes; de-dup the threat list.

---

## 4. Payments / Subscriptions

### PAY-1 — `/subscription/success` grants Paid on weak conditions *(High)*
`Controllers/SubscriptionController.cs:88-126`
The success page upgrades the **current user** to Paid whenever the passed `session_id` has `Status == "complete"`. It does **not** verify (a) that the session's `Metadata["userId"]` matches the current user, nor (b) `PaymentStatus == "paid"`. A user who can obtain any "complete" session id could self-upgrade. This is a redundant grant path alongside the webhook (the secure one).
**Fix:** Either remove the success-page grant and rely solely on the webhook, or verify `session.Metadata["userId"] == currentUserId` **and** `session.PaymentStatus == "paid"` before upgrading.

### PAY-2 — Webhook is not idempotent *(Medium)*
`SubscriptionController.cs:136-220`
Stripe can deliver the same event more than once. `SetPlanAsync` is mostly idempotent, but the side-effect emails (`SendPaymentSucceededAsync`, etc.) will re-send on duplicate delivery.
**Fix:** Persist processed Stripe event ids and skip duplicates (a small `ProcessedStripeEvents` table or a unique constraint).

### PAY-3 — `invoice.payment_failed` doesn't change plan state *(Medium)*
`SubscriptionController.cs:194-212`
On payment failure the code only emails the user; the subscription stays Active/Paid until `customer.subscription.deleted` arrives. This is defensible (Stripe dunning/retries), but it's an implicit policy with no record.
**Fix:** Decide and document the grace policy; optionally record a `PastDue` status so the UI/admin can reflect it.

### PAY-4 — `StripeConfiguration.ApiKey` set on a static per request *(Low)*
`SubscriptionController.cs:60, 98`
Setting a global static on each request works because the key is constant, but it's a code smell and fragile if keys ever vary.
**Fix:** Configure the Stripe client once at startup (or inject `IStripeClient`).

---

## 5. Logic & Flow

### LOGIC-1 — Creation-time rejections are never logged → auto-blacklist can't trigger *(High)*
`Controllers/PostsController.cs:236-243` vs `Services/ContentModerationService.cs:56-140`
When a post is hard-**Rejected** at creation, the controller adds a model error and returns the view **before** calling `_moderationLog.LogAsync`. So the rejection is never written to `ModerationLogs`, and `CheckAndBlacklistAsync` (which counts rejected logs) never sees it. Net effect: **a user can submit profanity/abuse endlessly and is never blacklisted** — the auto-blacklist feature only fires for items that were first saved as PendingReview and later rejected by the background job. (The same gap applies to the reply-create path.)
**Fix:** Call `LogAsync` for the rejected case too (with `contentId = 0` or a sentinel, since no row is saved), so rejections count toward the threshold. Verify the reply path mirrors this.

### LOGIC-2 — Manual moderation rejections aren't logged or notified *(High)*
`Controllers/ModerationController.cs:82-112`
Admin `Reject`/`Approve` update status directly but never call `ContentModerationService.LogAsync`, so manual decisions don't appear in `ModerationLogs`, don't count toward blacklisting, and the author is never emailed (unlike automated rejections, which do email via `LogAsync`). Inconsistent audit trail.
**Fix:** Route manual decisions through `LogAsync` (or a shared method) so they're audited and the author is notified.

### LOGIC-3 — Editing an approved post can silently hide it *(Medium)*
`PostsController.cs` Edit path (re-runs moderation on edit)
A trivial edit re-runs moderation and can flip an Approved post to PendingReview/Rejected, removing it from public view with no clear notice to the author.
**Fix:** Only downgrade visibility when moderation actually changes the verdict, and surface a clear message to the author when it does.

### LOGIC-4 — Users can react to / save Closed or Deleted posts *(Medium)*
`Controllers/ReactionsController.cs:37-39`, `Controllers/SavedPostsController.cs:41-46`
Toggle guards check `ModerationStatus == Approved` but not `Status == Active`, so reactions/saves are allowed on Closed/Deleted posts. SavedPosts also doesn't verify the post exists/visible (a bad `postId` throws an unhandled FK error → 500).
**Fix:** Add `post.Status == PostStatus.Active` to the guards; validate the post exists and is visible before insert.

### LOGIC-5 — `CanViewProfileAsync` distinct-owner limit is off-by-one / unclear *(Low)*
`Services/SubscriptionService.cs:66-73`
Free users are allowed when `distinctOwners <= 5`, which actually permits a 6th distinct owner depending on how you count. The intent ("5 per month") should be made explicit.
**Fix:** Use `< 5` (or `<= 4`) and add a comment / test pinning the intended limit.

---

## 6. Data Layer / EF Core

### DATA-1 — Multiple cascade paths on `PostReaction` / `SavedPost` may fail migration *(High)*
`Data/ApplicationDbContext.cs:107-117, 124-134`
Both entities cascade-delete from **both** `Post` and `User`. SQL Server rejects multiple cascade paths to the same table; a future migration/`EnsureCreated` against a fresh DB can fail.
**Fix:** Set one relationship per entity (e.g. the `User` side) to `DeleteBehavior.Restrict`/`NoAction`.

### DATA-2 — Fire-and-forget `Task.Run` with request-scoped services in `ApproveReaction` *(High)*
`Controllers/ReactionsController.cs:94-98`
The detached task uses tracked entities and scoped services tied to the request `DbContext`; after the request completes and the scope is disposed, this can throw `ObjectDisposedException` and the approval email is silently lost.
**Fix:** New scope via `IServiceScopeFactory` + pass primitives, or an awaited background queue. (Same root cause as AI-3 / ACC-6.)

### DATA-3 — Cartesian-explosion queries (multiple collection `Include`s) *(High)*
`Controllers/PostsController.cs:84-95` (Details), `Controllers/CaregiversController.cs:29-33` (Index), `Controllers/ProfileController.cs:44-49`
Each loads two or more collections on one root in a single query, multiplying rows (Replies × Reactions × Reviews, or Services × Reviews). Slow and memory-heavy as data grows.
**Fix:** Add `.AsSplitQuery()`, or (where the query ends in a `.Select` projection) drop the redundant `Include`s entirely and let EF generate subqueries.

### DATA-4 — `ProfileController` leaks phone unconditionally (inconsistent gating) *(High)*
`Controllers/ProfileController.cs:95-96`
Sets `Phone = profileUser.PhoneNumber` and `PhoneVisible = true` for any viewer who passes `CanViewProfileAsync`, with **no** paid-plan gate — unlike `CaregiversController.Details` which gates phone behind `IsPhoneVisibleAsync`. PII leak / monetization bypass.
**Fix:** Gate `Phone`/`PhoneVisible` behind the same `_subscription.IsPhoneVisibleAsync` check.

### DATA-5 — Missing `AsNoTracking()` on read-only queries *(Medium)*
`PostsController.cs:84-95`, `CaregiversController.cs:56-86`, `ProfileController.cs:44-49`
Display-only queries track entities unnecessarily.
**Fix:** Add `.AsNoTracking()` to read-only paths.

### DATA-6 — Reaction / saved-post toggles throw 500 on concurrent double-submit *(Medium)*
`ReactionsController.cs:45-67`, `SavedPostsController.cs:30-49`
Two concurrent toggles both see "no existing row", both insert, and the second `SaveChangesAsync` violates the unique `(PostId, UserId)` index → unhandled `DbUpdateException` → 500 instead of JSON.
**Fix:** Catch `DbUpdateException` and treat as already-applied (idempotent), or use an upsert.

### DATA-7 — In-memory `Take(5)` after loading all posts *(Medium)*
`Controllers/ProfileController.cs:46, 53-56`
`Include(u => u.Posts)` loads a user's entire post history, then `.Take(5)` filters in memory.
**Fix:** Query posts separately with server-side `Where/OrderByDescending/Take(5)` projection.

### DATA-8 — Missing index on `Post.Status` (and unbounded `nvarchar(max)` columns) *(Medium)*
`Data/ApplicationDbContext.cs:95-98`, entity files (`Post.City`, Stripe ids, `Profile` strings)
Heavy filtering on `Status` is unindexed; nullable strings without `[StringLength]` map to `nvarchar(max)` (can't be indexed, used in `Contains` filters).
**Fix:** Add `HasIndex(p => p.Status)` (or a composite `(Status, ModerationStatus, CreatedAt)`); add `[StringLength]` to `City`/address/Stripe-id columns and index `City` if filtered often.

### DATA-9 — No optimistic concurrency tokens *(Low)*
`Models/Entities/Post.cs`, `Reply.cs`, `Subscription.cs`
All updates are last-write-wins; concurrent edits/approvals overwrite each other undetected.
**Fix:** Add `[Timestamp] byte[] RowVersion` to entities updated concurrently.

### DATA-10 — Inconsistent rating average / city matching *(Low)*
`CaregiversController.cs:66` (`Average(r => r.Rating)` int) vs `:117` (`(double)r.Rating`); `:43` (`City ==`) vs `PostsController.cs:55` (`City.Contains`)
Inconsistent average casting and search semantics (exact vs contains, collation-dependent case sensitivity).
**Fix:** Cast `(double)` consistently; standardize city matching.

### DATA-11 — Log columns unbounded / unsanitized; error email can flood *(Low)*
`Services/AppLogService.cs:12-26`, `Middleware/ErrorLoggingMiddleware.cs:35,42`, `Services/NotificationService.cs:26-39`
`message`/`path`/`details` (full `ex.ToString()`) written verbatim with no length cap; stack traces may contain PII and can bloat the DB. Every unhandled exception also triggers a synchronous admin email with no dedup/throttle.
**Fix:** Truncate `Details`, cap message length, restrict log access, and debounce/aggregate the error email.

---

## 7. UI / UX

### UI-1 — Caregiver rating format bug: `ToString("0.1")` renders 4.7 as "4.1" *(High — visible data corruption)*
`Views/Caregivers/Index.cshtml:158`, `Views/Shared/_StarRating.cshtml:10`
`"0.1"` is a literal format ("digit, dot, literal 1"), not one-decimal. Every rated caregiver shows a wrong number after the decimal.
**Fix:** Use `ToString("0.0")` (or `"F1"`) in both places.

### UI-2 — AJAX reaction/save buttons have no loading or error state *(Medium)*
`Views/Posts/Details.cshtml:345-401`
On network failure the handlers just `return` with no feedback; no disabled/spinner state, so double-clicks fire duplicate toggles (compounds DATA-6).
**Fix:** Disable the button while awaiting; show an inline message/toast on `!resp.ok`.

### UI-3 — Reject-via-`prompt()` in Admin is fragile & inaccessible *(Medium)*
`Views/Admin/Posts.cshtml:72-73`
Uses a blocking `prompt()` for the rejection reason; empty/cancel silently aborts, not screen-reader friendly, and inconsistent with the nicer Bootstrap modal already used in `Views/Moderation/Queue.cshtml:139-161`.
**Fix:** Reuse the Moderation/Queue reject-modal pattern.

### UI-4 — Broken/dead anti-forgery fallback in reaction fetch *(Medium)*
`Views/Posts/Details.cshtml:348-349`
The fallback `|| '@Html.AntiForgeryToken()'` renders a full `<input>` HTML string, not a token value — it can never produce a valid token. Dead/incorrect code (works today only because a token-bearing form exists).
**Fix:** Remove the fallback; rely on the `querySelector('input[name="__RequestVerificationToken"]')` value already read.

### UI-5 — Hero/feature images hot-linked from Unsplash *(Medium)*
`Views/Home/Index.cshtml:7,59,68,77,86,95`
External `images.unsplash.com` URLs are a single point of failure (broken layout if blocked/changed), no `loading="lazy"`, hero background has no fallback.
**Fix:** Self-host under `wwwroot/images/`; add `loading="lazy"`.

### UI-6 — Accessibility gaps (icons, lang switcher, char counters) *(Low)*
`_Layout.cshtml:59-71` (no `aria-pressed`/`aria-current` on language buttons), decorative `<i class="fas ...">` icons throughout lack `aria-hidden="true"`, icon-only buttons (`Posts/Details.cshtml:106-116`) lack `aria-label`, and post/reply textareas lack the char counter that Support uses.
**Fix:** Add `aria-hidden`/`aria-label`/`aria-pressed`; add char counters to post/reply forms.

### UI-7 — Serialized reply body inlined into `onclick` (attribute-escape risk) *(Low)*
`Views/Posts/Details.cshtml:270`
`onclick="showReplyEdit(@reply.Id, @Html.Raw(JsonSerializer.Serialize(reply.Body)))"` inlines serialized user content into a double-quoted attribute; JSON's own double quotes can prematurely close the attribute in edge cases. (Largely mitigated by JSON's `<`/`>`/`&` escaping, but the pattern is risky.)
**Fix:** Use a `data-*` attribute (Razor auto-encodes) + `addEventListener` instead of inline `onclick`.

### UI-8 — Admin user pagination renders every page link *(Low)*
`Views/Admin/Users.cshtml:61`
One `<li>` per page with no windowing; unusable at scale.
**Fix:** Window the links (first/prev/±2/next/last).

### UI-9 — Hardcoded paths bypass routing *(Low)*
`Views/Posts/Details.cshtml:149,204`, `_DisclaimerPartial.cshtml:8`
`href="/profile/@id"`, `/disclaimer` etc. ignore path-base and break silently if routes change.
**Fix:** Use `asp-controller`/`asp-action` tag helpers.

### UI-10 — Orphaned `Template1–4.cshtml` and empty `site.js` *(Low)*
`Views/Home/Template1..4.cshtml`, `wwwroot/js/site.js`
Leftover scaffolding views not linked anywhere; `site.js` is empty (all JS is inline). Routing directly to a Template view could expose placeholder UI.
**Fix:** Delete the Template views; consolidate the duplicated inline token-fetch/error JS into `site.js`.

---

## 8. Localization (el/en)

The app injects `IStringLocalizer` and uses `@L["..."]` in places, but large swaths of user-facing text are hardcoded — mostly Greek — which defeats the en/el toggle.

### LOC-1 — Entire Admin & Moderation areas are hardcoded *(High)*
`Views/Admin/*.cshtml`, `Views/Moderation/Queue.cshtml`, `Logs.cshtml`
A mix of hardcoded Greek (Admin) and English (Moderation), no resource keys.
**Fix:** Move to resources (or consciously decide admin UI is single-language and document it).

### LOC-2 — Hardcoded strings throughout user-facing flows *(High)*
`Views/Posts/Details.cshtml` (multiple), `Dashboard/Index.cshtml:19,36-38`, `Dashboard/Settings.cshtml:113`, `Profile/Details.cshtml`, `Subscription/Index.cshtml:46,65`, `Account/VerifyEmailSent.cshtml`, `ConfirmEmailResult.cshtml`, `Home/Support.cshtml`
Plus server-side hardcoded Greek error messages in controllers (`PostsController.cs:225,232,317`, `SubscriptionController.cs:56`).
**Fix:** Replace with `@L[...]` / localized resources.

### LOC-3 — Footer/nav partially localized *(Medium)*
`_Layout.cshtml:104,109,120,181-184`
Some links use `@L`, siblings are hardcoded Greek.
**Fix:** Localize for consistency.

### LOC-4 — Register consent block hardcoded *(Medium)*
`Views/Account/Register.cshtml:92-95`
Terms-acceptance sentence + link text hardcoded Greek inside an otherwise localized form.
**Fix:** Localize.

### LOC-5 — Legal pages use inline `isEn ? ... : ...` instead of resources *(Low)*
`Views/Home/Privacy.cshtml` and other legal pages — workable for long copy, but won't scale past two languages.

---

## 9. Testing

### TEST-1 — No authorization tests for Admin/Moderation *(High)*
`frontida4baby.Tests/`
The suite covers webhook/subscription logic well but never asserts that non-admins are blocked from `AdminController`/`ModerationController`. For a PII platform this is the biggest coverage gap.
**Fix:** Add tests that a non-admin gets 403/redirect on admin/moderation actions.

### TEST-2 — Moderation pipeline untested *(Medium)*
Neither `WordlistModerationService`, `ContentModerationService`, the blacklist-threshold logic, nor the LOGIC-1 rejection-logging gap is tested.
**Fix:** Unit-test the wordlist (profanity/leet/word-boundary), and test that N rejections blacklist a user (this would have caught LOGIC-1).

### TEST-3 — Key flows untested *(Medium)*
No tests for: anti-forgery rejection on state-changing POSTs, error middleware logging, registration POST, support flow, webhook idempotency/concurrency, password reset (once added).
**Fix:** Add coverage incrementally, prioritizing security-relevant paths.

> **Positive:** `SubscriptionWebhookTests` is strong — checkout completion (new + upgrade), deletion downgrade, payment-failure, unknown events, and signature validation (missing/invalid/tampered → 400), including Stripe Invoice JSON-schema conformance. No live secrets in test fixtures (the `test_webhook_secret...` / `sk_test_placeholder` constants are clearly test-only).

---

## Implementation Plan

Work is sequenced by risk and dependency. Each phase is a self-contained, reviewable unit — do them on feature branches, one PR per phase.

### Phase 0 — Pre-deployment security gate *(blockers; ~0.5–1 day)*
Do these before any public/Production deployment.
1. **SEC-1** Remove hardcoded admin password → User Secrets/env, Dev-only auto-seed, fail-fast in Prod.
2. **SEC-2** Enable login lockout (`lockoutOnFailure: true` + `options.Lockout`).
3. **SEC-7 / SEC-8** Add `.gitignore` entries; confirm no secrets staged.
4. **PAY-1** Lock down `/subscription/success` (verify `userId` + `payment_status`) or remove the grant path.

### Phase 1 — Account flows *(~1–2 days)*
5. **ACC-1** Decide & implement email-confirmation enforcement (or gate sensitive actions on `EmailConfirmed`).
6. **ACC-2** Add forgot/reset-password (actions, views, email).
7. **ACC-3** Verify provider `email_verified` before auto-linking external logins.
8. **ACC-4** Fix token double-encoding.
9. **ACC-5** Resolve the frontida4all/frontida4baby brand mismatch (centralize the brand string).

### Phase 2 — AI moderation resilience (the "24/7" work) *(~2–3 days)*
10. **AI-4** Add HTTP resilience (retry + circuit breaker) to the Claude client.
11. **AI-3 / DATA-2 / ACC-6** Replace fire-and-forget `Task.Run` + scoped services with a proper background queue (or `IServiceScopeFactory` scopes). One shared fix.
12. **AI-2** Add per-item retry cap + per-run item cap + backoff to `PendingModerationJob`.
13. **AI-1** Add admin alerting when the Claude fallback path fires repeatedly.
14. **AI-5** Add health checks (`/health`, `/health/ready`) covering DB + Claude; wire an external uptime monitor + Sentry alerts.
15. **AI-6** Harden Claude JSON parsing (strip fences / extract object; log raw on failure).

### Phase 3 — Core logic & data integrity *(~2 days)*
16. **LOGIC-1** Log creation-time rejections so auto-blacklist works (add a regression test — TEST-2).
17. **LOGIC-2** Route manual moderation decisions through `LogAsync` (audit + notify).
18. **DATA-1** Fix multiple-cascade-path config.
19. **DATA-4** Gate `ProfileController` phone behind `IsPhoneVisibleAsync`.
20. **DATA-6 / LOGIC-4** Handle toggle race conditions + validate post status/existence in reactions & saved posts.
21. **DATA-3 / DATA-5 / DATA-7** Query fixes: `AsSplitQuery`, `AsNoTracking`, server-side `Take`.

### Phase 4 — Hardening & polish *(~2 days)*
22. **SEC-3 / SEC-4** Security headers + rate limiting (auth & webhook).
23. **SEC-5 / SEC-6** Strengthen password policy; set explicit `AllowedHosts`.
24. **PAY-2 / PAY-3** Webhook idempotency + payment-failure policy.
25. **DATA-8 / DATA-9 / DATA-11** Indexes, string lengths, concurrency tokens, log truncation.
26. **LOGIC-3 / LOGIC-5 / DATA-10 / AI-7** Edit-visibility, profile-limit off-by-one, average/city consistency, leet-map scoping.

### Phase 5 — UI/UX & localization *(~2–3 days, parallelizable)*
27. **UI-1** Rating format bug (quick win — do early).
28. **UI-2 / UI-3 / UI-4** AJAX states, admin reject modal, anti-forgery fallback.
29. **UI-5 / UI-8 / UI-9 / UI-10** Self-host images, paginate, tag-helper links, delete templates.
30. **UI-6 / UI-7** Accessibility + safe reply-edit binding.
31. **LOC-1..5** Localization sweep (largest effort; can be incremental).

### Phase 6 — Test coverage *(ongoing)*
32. **TEST-1** Admin/Moderation authorization tests.
33. **TEST-2** Wordlist + blacklist-threshold tests (pins LOGIC-1).
34. **TEST-3** Anti-forgery, error middleware, registration, webhook idempotency, password reset.

### Quick wins (batchable in one small PR today)
UI-1 (rating bug), SEC-7/SEC-8 (gitignore), AI-7 (dead duplicate threat entry), UI-4 (dead anti-forgery fallback), UI-10 (delete orphaned templates), ACC-5 (brand string).

---

## Notable positives (keep these)
- Two-stage moderation with an offline first pass and a manual fallback queue is a solid design.
- Stripe webhook signature verification is correct and well-tested.
- Email builders consistently HTML-encode user input — no email/HTML injection found.
- Secrets are externalized to User Secrets; no live secrets in source.
- Admin/Moderation controllers are correctly `[Authorize(Roles = "Admin")]`.
