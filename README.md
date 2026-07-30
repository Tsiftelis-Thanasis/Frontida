# frontida4baby (Φροντίδα)

A caregiving platform connecting families with trusted caregivers in Greece.

## Overview

frontida4baby (Φροντίδα = "care" in Greek) is a web application that helps families find verified babysitters, tutors, elderly caregivers, and other household help. It features community-driven posts, a moderation pipeline powered by Claude AI, Stripe-based subscriptions, and full Greek/English localization.

## Features

- **Community Posts**: Families post care requests; caregivers react and connect
- **Reaction Approval Flow**: Post owners approve caregivers who can then see contact details
- **Caregiver Listings**: Browse and search by service type and city
- **User Profiles**: Extended profiles with verification badges
- **Subscription Tiers**: Gated posting/replying/reacting via Stripe
- **AI Content Moderation**: 2-stage pipeline — wordlist pre-filter → Claude AI review
- **Admin Dashboard**: User management, moderation queue, payment oversight
- **Email Notifications**: Verification emails, admin alerts, user event notifications
- **Blacklist System**: Auto-blacklist after 3 content rejections; admin can unblacklist
- **Legal & Safety**: Terms of Service, Privacy Policy, Disclaimer, AI Review Policy pages
- **Cookie Consent**: GDPR-compliant cookie banner
- **Localization**: Greek (el) and English (en) via resource files

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core MVC |
| Database | PostgreSQL + Entity Framework Core |
| Auth | ASP.NET Core Identity + OAuth |
| Frontend | Bootstrap 5, Font Awesome 6, jQuery |
| Payments | Stripe |
| AI Moderation | Claude AI (Anthropic) |
| Email | SMTP (Gmail) |
| Hosting | Azure App Service |

## Prerequisites

- .NET 10 SDK
- PostgreSQL 16+ (or `docker run -e POSTGRES_PASSWORD=... postgres:16`)
- Visual Studio 2022 / VS Code with C# extension

## Getting Started

### 1. Clone

```bash
git clone https://github.com/Tsiftelis-Thanasis/Frontida.git
cd Frontida
```

### 2. Restore

```bash
dotnet restore
```

### 3. Configure

Set your connection string via user secrets (don't put it in `appsettings.json`):

```bash
cd frontida4baby.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=frontida4baby;Username=postgres;Password=your-local-password"
```

Set secrets for sensitive values (never commit these):

```bash
cd frontida4baby.Web
dotnet user-secrets set "Email:SmtpPass" "your-gmail-app-password"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Moderation:ClaudeApiKey" "sk-ant-..."
```

### 4. Migrate

```bash
cd frontida4baby.Web
dotnet ef database update
```

### 5. Run

```bash
dotnet run
# or for auto-reload:
dotnet watch run
```

Navigate to `https://localhost:5001`.

## Project Structure

```
frontida4baby/
├── Controllers/          # MVC controllers (thin, delegate to services)
├── Models/
│   ├── Entities/         # Domain models (ApplicationUser, Post, etc.)
│   ├── ViewModels/       # View-specific models
│   └── DTOs/             # Data transfer objects
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Migrations/
├── Services/             # Business logic
│   ├── ContentModerationService.cs
│   ├── NotificationService.cs
│   ├── UserEmailService.cs
│   ├── SubscriptionService.cs
│   └── PendingModerationJob.cs (hosted service)
├── Middleware/
│   └── ErrorLoggingMiddleware.cs
├── Resources/            # Localization (.resx) for el/en
├── Views/
│   ├── Account/
│   ├── Admin/
│   ├── Caregivers/
│   ├── Dashboard/
│   ├── Home/
│   ├── Moderation/
│   ├── Posts/
│   ├── Profile/
│   ├── Subscription/
│   └── Shared/
└── wwwroot/
    ├── css/
    └── js/
```

## Key Configuration (appsettings.json)

```json
{
  "Moderation": {
    "BlacklistThreshold": 3,
    "ClaudeApiKey": "...",
    "UseClaudeModeration": true
  },
  "Notifications": {
    "OnNewRegistration": true,
    "OnServerError": true,
    "OnUserBlacklisted": true,
    "OnPostRejected": true
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "AdminEmail": "frontida4all@gmail.com"
  }
}
```

## Roadmap

- [x] User authentication and authorization (Identity + OAuth)
- [x] Family and caregiver profiles
- [x] Community posts with service categories and city filter
- [x] Reaction & approval system
- [x] AI-powered content moderation (2-stage)
- [x] Admin dashboard (users, moderation queue, payments)
- [x] Stripe subscription tiers
- [x] Email verification flow
- [x] Admin notifications (registration, errors, blacklists, rejections)
- [x] Blacklist system with auto-trigger
- [x] Legal pages (Terms, Privacy, Disclaimer, AI Policy)
- [x] Greek/English localization
- [x] Cookie consent (GDPR)
- [x] Azure App Service deployment
- [ ] Real-time messaging (SignalR)
- [ ] Calendar integration for bookings
- [ ] Mobile apps (iOS/Android)
- [ ] Background check integration
- [ ] Advanced caregiver matching algorithm

## License

MIT License — see [LICENSE](LICENSE) for details.

## Contact

Support: frontida4all@gmail.com
Project: [https://github.com/Tsiftelis-Thanasis/Frontida](https://github.com/Tsiftelis-Thanasis/Frontida)
