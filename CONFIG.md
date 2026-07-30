# frontida4all — Configuration Reference

This file documents **every value you need to set** before going live.
Organised by where each value lives.

---

## 1. `appsettings.json` — Runtime Configuration

File: `frontida4baby.Web/appsettings.json`

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "..."        // ← PostgreSQL connection string (see §1.1)
  },

  "Moderation": {
    "ClaudeApiKey":        "...",     // ← Anthropic API key (see §1.2)
    "ClaudeModel":         "claude-haiku-4-5-20251001",  // model to use for moderation
    "MaxTokens":           256,       // max tokens per moderation response (keep low)
    "TimeoutSeconds":      10,        // seconds before Claude call is abandoned
    "FallbackOnTimeout":   "PendingReview", // "Approved" | "Rejected" | "PendingReview"
    "BlacklistThreshold":  3          // rejections before auto-blacklist (see §1.3)
  },

  "Stripe": {
    "Enabled":        true,           // false = disables all payment flows
    "SecretKey":      "sk_live_...",  // ← Stripe secret key (see §1.4)
    "PublishableKey": "pk_live_...",  // ← Stripe publishable key
    "PaidPriceId":    "price_...",    // ← Stripe Price ID for the Premium plan
    "WebhookSecret":  "whsec_..."     // ← Stripe webhook signing secret
  },

  "Email": {
    "SmtpHost":    "smtp.gmail.com",  // SMTP server hostname
    "SmtpPort":    587,               // SMTP port (587 = STARTTLS, 465 = SSL)
    "FromAddress": "noreply@yourplatform.gr",  // ← sender address shown to users
    "FromName":    "frontida4all",    // ← sender name shown to users
    "AdminEmail":  "admin@yourplatform.gr",    // ← receives new-user & error alerts
    "Username":    "...",             // ← SMTP login username (usually same as FromAddress)
    "Password":    "..."              // ← SMTP password / app password (use secrets, see §2)
  }
}
```

### 1.1 Database connection string

Place in `appsettings.json` (or user secrets / environment variable for production):

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=YOUR_HOST;Port=5432;Database=frontida4baby;Username=YOUR_USER;Password=YOUR_PASS"
}
```

For a managed Postgres host requiring SSL (Supabase, Neon, Railway, etc.), add `SSL Mode=Require;Trust Server Certificate=true`.

### 1.2 Claude / Anthropic API key

1. Go to <https://console.anthropic.com/> → API Keys → Create key
2. Set `Moderation:ClaudeApiKey` in appsettings or secrets (see §2)
3. If left empty the system falls back to `FallbackOnTimeout` decision for every post

`FallbackOnTimeout` values:
| Value | Behaviour |
|---|---|
| `"PendingReview"` | Post goes to admin queue (default — safest) |
| `"Approved"` | Post is published immediately (use only in dev) |
| `"Rejected"` | Post is always blocked (disables posting without AI) |

### 1.3 Blacklist threshold

`Moderation:BlacklistThreshold` (default `3`) — number of content rejections that trigger
an automatic account blacklist. Increase to be more lenient, decrease to be stricter.
Admin can always unblacklist from Admin → Users → UserDetail.

### 1.4 Stripe keys

| Key | Where to find it |
|---|---|
| `SecretKey` | Stripe Dashboard → Developers → API keys → Secret key |
| `PublishableKey` | Stripe Dashboard → Developers → API keys → Publishable key |
| `PaidPriceId` | Stripe Dashboard → Products → create a product → copy Price ID |
| `WebhookSecret` | Stripe Dashboard → Developers → Webhooks → your endpoint → Signing secret |

Webhook endpoint to register in Stripe: `https://yourplatform.gr/subscription/webhook`

Set `Stripe:Enabled` to `false` during development to skip all payment flows.

---

## 2. Secrets (never commit to git)

For **development**, use .NET user secrets:

```bash
cd frontida4baby.Web
dotnet user-secrets set "Moderation:ClaudeApiKey" "sk-ant-..."
dotnet user-secrets set "Email:Password"           "your-smtp-app-password"
dotnet user-secrets set "Stripe:SecretKey"         "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret"     "whsec_..."
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=5432;Database=...;Username=...;Password=..."
```

For **production**, set environment variables (or use Azure Key Vault / AWS Secrets Manager):

```
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=...;Username=...;Password=...
Moderation__ClaudeApiKey=sk-ant-...
Email__Password=...
Stripe__SecretKey=sk_live_...
Stripe__WebhookSecret=whsec_...
```

---

## 3. `Program.cs` — Seed Admin Account (hardcoded)

File: `frontida4baby.Web/Program.cs` lines ~112–126

```csharp
const string adminEmail    = "admin@frontida4baby.gr";  // ← change before first run
const string adminPassword = "Admin1234!";              // ← CHANGE THIS — minimum 6 chars,
                                                        //   uppercase + digit required
```

**This user is auto-created once** (only if the email doesn't exist yet). After first run,
change the password in the Admin panel or delete and recreate the user.

> ⚠️ The seed credentials are in plain source code. Move them to appsettings/secrets before deploying.

---

## 4. Legal Pages — Contact Emails (in view files)

These are static strings inside Razor views. Edit them to match your real email addresses.

| Email address | File | Line | Purpose |
|---|---|---|---|
| `support@frontida4all.gr` | `Views/Home/Disclaimer.cshtml` | 71 | General contact / complaints |
| `support@frontida4all.gr` | `Views/Home/AiReviewPolicy.cshtml` | 107 | AI moderation appeals |
| `support@frontida4all.gr` | `Views/Home/TermsOfService.cshtml` | 87 | Blacklist removal requests |
| `privacy@frontida4all.gr` | `Views/Home/Privacy.cshtml` | 136, 161 | GDPR rights requests |
| `legal@frontida4all.gr` | `Views/Home/TermsOfService.cshtml` | 150 | Legal / ToS enquiries |

Quick global replace — run from the repo root:

```bash
# Replace placeholder domain in all views
find frontida4baby.Web/Views -name "*.cshtml" \
  -exec sed -i 's/frontida4all\.gr/yourplatform.gr/g' {} \;
```

Or open each file in an editor and search for `frontida4all.gr`.

---

## 5. Legal Pages — Other Placeholder Text

These strings are in the same view files and need updating for your actual business:

| Placeholder | File | What to change |
|---|---|---|
| `"Τελευταία ενημέρωση: Μάρτιος 2026"` | All 4 legal views | Update date whenever you revise the page |
| `"Διεύθυνση: Ελλάδα"` | `Privacy.cshtml` line 162 | Your full registered business address |
| `"αρμόδια είναι αποκλειστικά τα δικαστήρια της Αθήνας"` | `TermsOfService.cshtml` line 140 | Your jurisdiction / court location |
| `"Υπεύθυνος Επεξεργασίας: frontida4all"` | `Privacy.cshtml` line 160 | Your legal entity name |

---

## 6. Free-plan Limits (hardcoded in `SubscriptionService.cs`)

File: `frontida4baby.Web/Services/SubscriptionService.cs`

These are compiled values, not config — edit the source to change them:

| Limit | Method | Current value |
|---|---|---|
| Posts per month (free) | `CanPostAsync` | `5` |
| Replies per month (free) | `CanReplyAsync` | `20` |
| Reactions per month (free) | `CanReactAsync` | `10` |
| Profiles viewable per month (free) | `CanViewProfileAsync` | `5` |

---

## 7. Notification System

### 7.1 Gmail setup (recommended)

All admin notifications are sent via SMTP. To use Gmail:

**Step 1 — Enable 2-Step Verification on your Google account**
<https://myaccount.google.com/security>

**Step 2 — Create a Gmail App Password**
1. Go to <https://myaccount.google.com/apppasswords>
2. Select app: **Mail**, device: **Other** → type `frontida4all`
3. Click **Generate** — copy the 16-character password (e.g. `abcd efgh ijkl mnop`)
4. Store it as a secret (see §2): `dotnet user-secrets set "Email:SmtpPass" "abcdefghijklmnop"`

**Step 3 — Fill in `appsettings.json`**

```json
"Email": {
  "SmtpHost":    "smtp.gmail.com",
  "SmtpPort":    587,
  "FromAddress": "you@gmail.com",
  "FromName":    "frontida4all",
  "SmtpUser":    "you@gmail.com",
  "SmtpPass":    "",             ← use secrets, not here
  "AdminEmail":  "you@gmail.com"
}
```

> `FromAddress`, `SmtpUser`, and `AdminEmail` can all be the same Gmail address.

**Other SMTP providers:**

| Provider | SmtpHost | SmtpPort |
|---|---|---|
| Gmail | `smtp.gmail.com` | 587 |
| Outlook/Hotmail | `smtp-mail.outlook.com` | 587 |
| SendGrid | `smtp.sendgrid.net` | 587 |
| Brevo (Sendinblue) | `smtp-relay.brevo.com` | 587 |
| Mailgun | `smtp.mailgun.org` | 587 |

---

### 7.2 Notification event toggles

Configured in `appsettings.json` under the `Notifications` section:

```json
"Notifications": {
  "ServerErrors":    true,   ← email on every unhandled 500 exception
  "NewRegistration": true,   ← email when a new user registers
  "UserBlacklisted": true,   ← email when auto-blacklist fires
  "PostRejected":    false   ← email on every rejected post/reply (noisy, off by default)
}
```

Set any to `false` to silence that category. All are handled by
`NotificationService` (`Services/NotificationService.cs`).

---

### 7.3 What each notification looks like

| Event | Subject line | Colour |
|---|---|---|
| Server error | `[ERROR] ExceptionType: message` | Red |
| New registration | `[NEW USER] email@example.com` | Purple |
| User blacklisted | `[BLACKLIST] email@example.com` | Amber |
| Post rejected | `[REJECTED] content snippet…` | Grey |

---

### 7.4 Troubleshooting: no emails arriving

1. **Check `Email:SmtpUser` is not empty** — `SmtpEmailService` skips sending silently if it is
2. **Check `Email:AdminEmail` is not empty** — `NotificationService` logs a warning and skips
3. **Gmail "Less secure app" block** — use an App Password (§7.1), not your account password
4. **Check spam folder** — Gmail may initially send its own outbound to spam
5. **Check the app logs** — Admin → Logs → filter by `Error` for SMTP failures
6. **Test with a real error** — visit `/Home/Error` or temporarily throw in a controller action

---

## 8. Brand Name & Domain

The site name `"frontida4all"` appears in several places that are not localisation-keyed:

| Location | File |
|---|---|
| `<title>` tag | `Views/Shared/_Layout.cshtml` line 6 |
| Navbar brand | `_Layout.cshtml` line 24 |
| Footer brand | `_Layout.cshtml` lines 153, 174 |
| Legal page body text | All 4 legal views |
| Email `FromName` | `appsettings.json` |
| Seed admin email | `Program.cs` line 112 |

---

## 9. Email Verification Flow

Added in: March 2026.

### How it works
1. User registers → account created + signed in immediately (non-blocking)
2. Verification email is sent automatically in the background
3. Until verified: a dismissible yellow banner appears on the Dashboard with a "Send again" button
4. User clicks the link in the email → `/account/confirm-email?userId=…&token=…`
5. On success: `EmailConfirmed = true` in `AspNetUsers`; verified badge appears on profile

### What to configure

| Key | Where | Purpose |
|---|---|---|
| `Email:SmtpHost` | `appsettings.json` | SMTP server (required for email to send) |
| `Email:SmtpPort` | `appsettings.json` | Port — 587 for STARTTLS, 465 for SSL |
| `Email:FromAddress` | `appsettings.json` | "From" address in the verification email |
| `Email:FromName` | `appsettings.json` | "From" display name, e.g. `"frontida4all"` |
| `Email:Username` | secrets | SMTP login (usually same as FromAddress) |
| `Email:Password` | secrets | SMTP password / app password |

> The confirmation link is built using `Request.Scheme + Request.Host` at send time,
> so no separate "base URL" config is needed — it will use the current request's host automatically.
> In development behind a reverse proxy, ensure `UseForwardedHeaders` is configured if needed.

### Email template
The HTML email is hardcoded in `AccountController.SendVerificationEmailAsync`.
To customise the email text, edit that private method (around line 270 of `AccountController.cs`).
Key things you may want to change:
- The greeting line (`Γεια σου {FirstName}`)
- The button label (`Επιβεβαίωση Email`)
- The footer line (currently shows `{FromName} · Greece`)
- The expiry notice (currently says 24 hours — ASP.NET Identity default is actually **15 days**)

To change the token lifetime, add to `Program.cs` in the Identity options:
```csharp
options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
// or configure DataProtectionTokenProviderOptions:
builder.Services.Configure<DataProtectionTokenProviderOptions>(o =>
    o.TokenLifespan = TimeSpan.FromDays(1));
```

### Verified badge
Shown on the public profile page (`/profile/{userId}`) as a small green
`✓ Επιβεβαιωμένος` badge next to the user's name.
It reads from `ApplicationUser.EmailConfirmed` (standard Identity field, no migration needed).

### Dashboard banner
Shown on `/dashboard` whenever `EmailConfirmed = false`.
It includes an inline "Send again" form that calls `POST /account/resend-verification`.
The banner is dismissible (Bootstrap `alert-dismissible`) — dismissal is per-page-load only
(no cookie persistence), so it reappears on next visit until the email is confirmed.

### OAuth / social login users
Users who register via Google, Facebook, etc. have `EmailConfirmed = true` set automatically
at account creation (see `AccountController.ExternalLoginCallback`). They skip the verification
flow entirely and do not see the dashboard banner.

---

## 10. Quick Checklist — Before Going Live

- [ ] Set `ConnectionStrings:DefaultConnection` (use secrets)
- [ ] Configure SMTP (`Email:SmtpHost/Port/FromAddress/FromName/Username/Password`) so verification emails send
- [ ] Set `Moderation:ClaudeApiKey` (use secrets)
- [ ] Change seed admin email & password in `Program.cs`
- [ ] Set `Email:SmtpHost`, `SmtpPort`, `FromAddress`, `FromName`, `Username`, `Password`
- [ ] Set `Email:AdminEmail` to receive alerts
- [ ] Configure Stripe keys (or set `Stripe:Enabled: false` to skip)
- [ ] Replace `frontida4all.gr` in all legal views with your real domain
- [ ] Update business address and jurisdiction text in `Privacy.cshtml` and `TermsOfService.cshtml`
- [ ] Update "Τελευταία ενημέρωση" date in all 4 legal views
- [ ] Review `Moderation:BlacklistThreshold` (default: 3)
- [ ] Review free-plan limits in `SubscriptionService.cs`
- [ ] Run `dotnet ef database update` on the production server
