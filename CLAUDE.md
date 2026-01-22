# Frontida - Project Context for Claude

## Project Overview
Frontida (Φροντίδα - Greek for "care") is a simplified caregiving platform that connects families with verified caregivers in Greece. This is a streamlined alternative to platforms like Nannuka, focusing on simplicity and core functionality.

## Technical Stack
- **Framework**: .NET 10
- **Database**: SQL Server
- **ORM**: Entity Framework Core
- **Frontend**: ASP.NET Core MVC/Razor Pages
- **Authentication**: ASP.NET Core Identity

## Core Features
1. **User Management**
   - Family accounts (seeking care)
   - Caregiver accounts (providing services)
   - Profile creation and verification
   - ASP.NET Core Identity for authentication

2. **Service Categories**
   - Childcare (babysitters, nannies)
   - Tutoring (academic support)
   - Elderly care
   - Housekeeping
   - Pet care

3. **Search & Discovery**
   - Browse caregivers by service type
   - Filter by location, availability, and ratings
   - View detailed caregiver profiles

4. **Booking System**
   - Schedule appointments
   - Manage bookings
   - Booking status tracking

5. **Reviews & Trust**
   - Rating system
   - Written reviews
   - Verification badges

6. **Messaging**
   - Direct messaging between families and caregivers
   - Inquiry management

## Database Schema

### Core Tables
- **Users** (ASP.NET Identity)
- **Profiles** (Extended user information)
  - FirstName, LastName
  - Phone, Address, City
  - UserType (Family/Caregiver)
  - IsVerified, VerificationDate
  - Bio, ProfilePicture

- **Services**
  - ServiceId, ServiceName
  - Category, Description
  - PriceRange

- **CaregiverServices** (Many-to-many)
  - Links caregivers to services they offer
  - HourlyRate, Experience, Availability

- **Bookings**
  - BookingId, FamilyId, CaregiverId
  - ServiceId, BookingDate, StartTime, EndTime
  - Status (Pending/Confirmed/Completed/Cancelled)
  - TotalAmount

- **Reviews**
  - ReviewId, BookingId, Rating (1-5)
  - Comment, CreatedDate

- **Messages**
  - MessageId, SenderId, ReceiverId
  - Content, SentDate, IsRead

## Development Guidelines

### Code Organization
- Follow repository pattern for data access
- Use services for business logic
- Keep controllers thin
- Use DTOs for data transfer
- Implement proper validation

### Naming Conventions
- PascalCase for classes, methods, properties
- camelCase for local variables and parameters
- Descriptive names (avoid abbreviations)
- Use Greek terms where culturally appropriate

### Security Considerations
- Validate all user inputs
- Use parameterized queries (EF Core handles this)
- Implement proper authorization checks
- Secure sensitive data (passwords, personal info)
- HTTPS only in production

### Localization
- Support Greek (el) and English (en)
- Use resource files for strings
- Date/time formatting for Greek locale

## Project Structure
```
Frontida/
├── Controllers/
│   ├── HomeController.cs
│   ├── AccountController.cs
│   ├── ProfileController.cs
│   ├── CaregiverController.cs
│   ├── BookingController.cs
│   └── MessageController.cs
├── Models/
│   ├── Domain/          # Entity models
│   ├── ViewModels/      # View-specific models
│   └── DTOs/            # Data transfer objects
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Migrations/
├── Services/
│   ├── Interfaces/
│   └── Implementations/
├── Repositories/
│   ├── Interfaces/
│   └── Implementations/
├── Views/
│   ├── Home/
│   ├── Account/
│   ├── Profile/
│   ├── Caregiver/
│   └── Shared/
└── wwwroot/
    ├── css/
    ├── js/
    └── images/
```

## Development Priorities
1. Set up project structure and database
2. Implement user authentication and authorization
3. Create profile management (family and caregiver)
4. Build caregiver search and listing
5. Develop booking system
6. Add messaging functionality
7. Implement reviews and ratings
8. Polish UI/UX with Greek localization
9. Add verification system
10. Implement payment processing (future)

## Design Philosophy
- **Simplicity First**: Keep the UI clean and intuitive
- **Mobile-Friendly**: Responsive design for all devices
- **Trust & Safety**: Emphasize verification and reviews
- **Local Focus**: Tailored for the Greek market
- **Fast Performance**: Optimize queries and page loads

## Common Commands
```bash
# Create new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Run application
dotnet run

# Run with watch (auto-reload)
dotnet watch run

# Build for production
dotnet publish -c Release
```

## Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development/Production
- Connection strings in appsettings.json
- Email service credentials (for verification)
- File storage configuration (profile pictures)

## Testing Strategy
- Unit tests for services and business logic
- Integration tests for database operations
- E2E tests for critical user flows
- Use xUnit as testing framework

## Known Considerations
- GDPR compliance for user data
- Greek data protection laws
- Secure storage of sensitive information
- Background checks for caregiver verification
- Insurance and liability considerations

## Future Enhancements
- Mobile apps (iOS/Android)
- Real-time chat with SignalR
- Calendar integration
- Payment processing
- Background check integration
- Subscription tiers for caregivers
- Advanced matching algorithm
- Video profiles for caregivers
