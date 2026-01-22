# Frontida Development Tasks - GitHub Issues

Copy each task below as a separate GitHub Issue. Tag with appropriate labels: `enhancement`, `bug`, `documentation`, `setup`, `database`, `backend`, `frontend`, `security`.

---

## Phase 1: Project Setup & Infrastructure

### Issue #1: Initial .NET 10 Project Setup
**Labels:** `setup`, `enhancement`

**Description:**
Set up the initial .NET 10 MVC project structure for Frontida.

**Tasks:**
- [ ] Create new .NET 10 MVC project
- [ ] Configure project structure (Controllers, Models, Views, Data, Services)
- [ ] Set up dependency injection container
- [ ] Configure launchSettings.json for development
- [ ] Add .gitignore for .NET projects
- [ ] Set up appsettings.json and appsettings.Development.json

**Acceptance Criteria:**
- Project builds successfully with `dotnet build`
- Project runs with `dotnet run`
- Proper folder structure is in place

---

### Issue #2: Database Configuration & Context Setup
**Labels:** `setup`, `database`, `enhancement`

**Description:**
Configure SQL Server connection and set up Entity Framework Core DbContext.

**Tasks:**
- [ ] Install EF Core NuGet packages (Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools)
- [ ] Create ApplicationDbContext class
- [ ] Configure connection string in appsettings.json
- [ ] Set up DbContext in Program.cs dependency injection
- [ ] Create initial migration
- [ ] Test database connection

**Acceptance Criteria:**
- Database connection is successful
- DbContext is properly configured
- Initial migration creates database

---

### Issue #3: ASP.NET Core Identity Setup
**Labels:** `setup`, `security`, `enhancement`

**Description:**
Implement ASP.NET Core Identity for user authentication and authorization.

**Tasks:**
- [ ] Install Microsoft.AspNetCore.Identity.EntityFrameworkCore
- [ ] Configure Identity in ApplicationDbContext
- [ ] Set up Identity services in Program.cs
- [ ] Configure password requirements
- [ ] Create IdentityUser extension for custom fields
- [ ] Add Identity migrations

**Acceptance Criteria:**
- Identity tables are created in database
- User registration is possible
- Login/logout functionality works

---

## Phase 2: Core Domain Models

### Issue #4: Create User Profile Models
**Labels:** `database`, `backend`, `enhancement`

**Description:**
Create domain models for user profiles (Family and Caregiver).

**Tasks:**
- [ ] Create UserProfile entity (extends IdentityUser)
- [ ] Add properties: FirstName, LastName, Phone, Address, City, Bio
- [ ] Add UserType enum (Family, Caregiver)
- [ ] Add IsVerified, VerificationDate fields
- [ ] Create ProfilePicture field (file path)
- [ ] Add DateCreated, LastUpdated timestamps
- [ ] Create and apply migration

**Acceptance Criteria:**
- UserProfile table exists in database
- All fields are properly typed and nullable as appropriate
- Migration runs without errors

---

### Issue #5: Create Service & Category Models
**Labels:** `database`, `backend`, `enhancement`

**Description:**
Create models for services offered by caregivers.

**Tasks:**
- [ ] Create ServiceCategory enum (Childcare, Tutoring, ElderlyCare, Housekeeping, PetCare)
- [ ] Create Service entity (ServiceId, ServiceName, Category, Description)
- [ ] Create PriceRange value object
- [ ] Add indexes for frequently queried fields
- [ ] Create and apply migration

**Acceptance Criteria:**
- Service table exists with proper relationships
- Categories are properly enumerated
- Sample services can be seeded

---

### Issue #6: Create CaregiverService Junction Model
**Labels:** `database`, `backend`, `enhancement`

**Description:**
Create many-to-many relationship between caregivers and services.

**Tasks:**
- [ ] Create CaregiverService entity
- [ ] Add CaregiverId, ServiceId foreign keys
- [ ] Add HourlyRate, Experience (years), Availability fields
- [ ] Configure many-to-many relationship in DbContext
- [ ] Add composite key (CaregiverId, ServiceId)
- [ ] Create and apply migration

**Acceptance Criteria:**
- Junction table properly links caregivers to services
- Caregivers can offer multiple services
- Additional metadata is stored per service offering

---

### Issue #7: Create Booking Model
**Labels:** `database`, `backend`, `enhancement`

**Description:**
Create booking/appointment model for scheduling care services.

**Tasks:**
- [ ] Create Booking entity
- [ ] Add FamilyId, CaregiverId, ServiceId foreign keys
- [ ] Add BookingDate, StartTime, EndTime, Duration fields
- [ ] Create BookingStatus enum (Pending, Confirmed, Completed, Cancelled)
- [ ] Add TotalAmount, Notes fields
- [ ] Add CreatedDate, LastModified timestamps
- [ ] Configure relationships in DbContext
- [ ] Create and apply migration

**Acceptance Criteria:**
- Booking table exists with all relationships
- Status transitions are properly defined
- Timestamps are auto-managed

---

### Issue #8: Create Review Model
**Labels:** `database`, `backend`, `enhancement`

**Description:**
Create review and rating system for completed bookings.

**Tasks:**
- [ ] Create Review entity
- [ ] Add BookingId, ReviewerId foreign keys
- [ ] Add Rating field (1-5 scale)
- [ ] Add Comment field (text)
- [ ] Add CreatedDate, IsVerified fields
- [ ] Configure one-to-one relationship with Booking
- [ ] Create and apply migration

**Acceptance Criteria:**
- Review table exists
- Each booking can have one review
- Rating validation is in place

---

### Issue #9: Create Message Model
**Labels:** `database`, `backend`, `enhancement`

**Description:**
Create messaging system for communication between users.

**Tasks:**
- [ ] Create Message entity
- [ ] Add SenderId, ReceiverId foreign keys
- [ ] Add Content, SentDate fields
- [ ] Add IsRead, ReadDate fields
- [ ] Add ConversationId for grouping
- [ ] Create indexes for query optimization
- [ ] Create and apply migration

**Acceptance Criteria:**
- Message table exists
- Messages can be grouped by conversation
- Read status tracking works

---

## Phase 3: Repository & Service Layer

### Issue #10: Create Generic Repository Pattern
**Labels:** `backend`, `enhancement`

**Description:**
Implement generic repository pattern for data access.

**Tasks:**
- [ ] Create IRepository<T> interface
- [ ] Implement Repository<T> base class
- [ ] Add methods: GetById, GetAll, Find, Add, Update, Delete
- [ ] Add async versions of all methods
- [ ] Create IUnitOfWork interface
- [ ] Implement UnitOfWork class
- [ ] Register in dependency injection

**Acceptance Criteria:**
- Generic repository reduces code duplication
- All CRUD operations are available
- Unit of Work pattern is implemented

---

### Issue #11: Create Profile Service
**Labels:** `backend`, `enhancement`

**Description:**
Create service layer for user profile management.

**Tasks:**
- [ ] Create IProfileService interface
- [ ] Implement ProfileService class
- [ ] Add CreateProfile, UpdateProfile, GetProfile methods
- [ ] Add GetCaregiverProfile, GetFamilyProfile methods
- [ ] Add profile picture upload handling
- [ ] Implement validation logic
- [ ] Add error handling
- [ ] Register service in DI

**Acceptance Criteria:**
- Profile CRUD operations work
- Business logic is separated from controllers
- Validation prevents invalid data

---

### Issue #12: Create Caregiver Search Service
**Labels:** `backend`, `enhancement`

**Description:**
Implement service for searching and filtering caregivers.

**Tasks:**
- [ ] Create ICaregiverSearchService interface
- [ ] Implement CaregiverSearchService class
- [ ] Add SearchByCategory method
- [ ] Add SearchByLocation method (city)
- [ ] Add FilterByRating method
- [ ] Add FilterByAvailability method
- [ ] Implement pagination
- [ ] Add sorting options (rating, price, experience)

**Acceptance Criteria:**
- Caregivers can be searched by multiple criteria
- Results are paginated
- Search is performant with proper indexes

---

### Issue #13: Create Booking Service
**Labels:** `backend`, `enhancement`

**Description:**
Implement booking management service.

**Tasks:**
- [ ] Create IBookingService interface
- [ ] Implement BookingService class
- [ ] Add CreateBooking method with validation
- [ ] Add CancelBooking method
- [ ] Add ConfirmBooking method
- [ ] Add GetUpcomingBookings method
- [ ] Add GetBookingHistory method
- [ ] Implement availability checking
- [ ] Add conflict detection

**Acceptance Criteria:**
- Bookings can be created and managed
- Double-booking is prevented
- Status transitions are validated

---

### Issue #14: Create Review Service
**Labels:** `backend`, `enhancement`

**Description:**
Implement review and rating service.

**tasks:**
- [ ] Create IReviewService interface
- [ ] Implement ReviewService class
- [ ] Add CreateReview method (only for completed bookings)
- [ ] Add GetCaregiverReviews method
- [ ] Add CalculateAverageRating method
- [ ] Prevent duplicate reviews
- [ ] Add moderation flag capability

**Acceptance Criteria:**
- Only completed bookings can be reviewed
- Average ratings are calculated correctly
- Duplicate reviews are prevented

---

### Issue #15: Create Message Service
**Labels:** `backend`, `enhancement`

**Description:**
Implement messaging service for user communication.

**Tasks:**
- [ ] Create IMessageService interface
- [ ] Implement MessageService class
- [ ] Add SendMessage method
- [ ] Add GetConversation method
- [ ] Add MarkAsRead method
- [ ] Add GetUnreadCount method
- [ ] Implement conversation grouping

**Acceptance Criteria:**
- Users can send and receive messages
- Conversations are properly grouped
- Unread count is accurate

---

## Phase 4: Controllers & Views

### Issue #16: Create Account Controller
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Implement user registration, login, and account management.

**Tasks:**
- [ ] Create AccountController
- [ ] Add Register action (GET/POST)
- [ ] Add Login action (GET/POST)
- [ ] Add Logout action
- [ ] Add ForgotPassword action
- [ ] Add ResetPassword action
- [ ] Create corresponding views
- [ ] Add client-side validation
- [ ] Implement reCAPTCHA (optional)

**Acceptance Criteria:**
- Users can register and login
- Password reset works
- Forms have proper validation

---

### Issue #17: Create Profile Controller
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Implement profile viewing and editing functionality.

**Tasks:**
- [ ] Create ProfileController
- [ ] Add ViewProfile action
- [ ] Add EditProfile action (GET/POST)
- [ ] Add UploadPhoto action
- [ ] Create profile view (different for Family/Caregiver)
- [ ] Create edit profile form
- [ ] Add image upload handling
- [ ] Implement authorization (users can only edit own profile)

**Acceptance Criteria:**
- Users can view and edit their profiles
- Photo upload works
- Authorization prevents unauthorized edits

---

### Issue #18: Create Caregiver Controller
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Implement caregiver search, browse, and profile viewing.

**Tasks:**
- [ ] Create CaregiverController
- [ ] Add Search action with filters
- [ ] Add Browse action (list all)
- [ ] Add ViewCaregiver action (detailed profile)
- [ ] Create search view with filters
- [ ] Create caregiver list view with pagination
- [ ] Create detailed caregiver profile view
- [ ] Add favorite/bookmark functionality (optional)

**Acceptance Criteria:**
- Families can search and browse caregivers
- Filters work correctly
- Pagination is smooth

---

### Issue #19: Create Booking Controller
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Implement booking creation and management.

**Tasks:**
- [ ] Create BookingController
- [ ] Add CreateBooking action (GET/POST)
- [ ] Add MyBookings action (list)
- [ ] Add BookingDetails action
- [ ] Add CancelBooking action
- [ ] Add ConfirmBooking action (for caregivers)
- [ ] Create booking form view
- [ ] Create my bookings list view
- [ ] Create booking details view
- [ ] Add date/time picker

**Acceptance Criteria:**
- Families can create bookings
- Caregivers can confirm/decline
- Both parties can view their bookings

---

### Issue #20: Create Review Controller
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Implement review submission and viewing.

**Tasks:**
- [ ] Create ReviewController
- [ ] Add CreateReview action (GET/POST)
- [ ] Add CaregiverReviews action (list for a caregiver)
- [ ] Create review submission form
- [ ] Create reviews display view
- [ ] Add star rating component
- [ ] Prevent reviewing before booking completion

**Acceptance Criteria:**
- Families can review completed bookings
- Reviews display with ratings
- Only completed bookings can be reviewed

---

### Issue #21: Create Message Controller
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Implement messaging interface.

**Tasks:**
- [ ] Create MessageController
- [ ] Add Inbox action (conversation list)
- [ ] Add Conversation action (message thread)
- [ ] Add SendMessage action (POST)
- [ ] Create inbox view
- [ ] Create conversation view
- [ ] Add real-time updates (SignalR - optional)
- [ ] Add notification badge for unread messages

**Acceptance Criteria:**
- Users can send and receive messages
- Conversations are threaded
- Unread messages are highlighted

---

### Issue #22: Create Home Controller & Landing Page
**Labels:** `frontend`, `enhancement`

**Description:**
Create homepage with search and featured caregivers.

**Tasks:**
- [ ] Create/update HomeController
- [ ] Add Index action with featured caregivers
- [ ] Add About action
- [ ] Add Contact action
- [ ] Create modern, responsive landing page
- [ ] Add quick search component
- [ ] Add service category cards
- [ ] Add Greek localization
- [ ] Optimize for mobile

**Acceptance Criteria:**
- Landing page is attractive and functional
- Quick search works
- Page is responsive

---

## Phase 5: UI/UX & Localization

### Issue #23: Implement Bootstrap/Tailwind Styling
**Labels:** `frontend`, `enhancement`

**Description:**
Apply consistent styling throughout the application.

**Tasks:**
- [ ] Choose CSS framework (Bootstrap or Tailwind)
- [ ] Set up framework in project
- [ ] Create consistent layout template
- [ ] Style all forms consistently
- [ ] Add responsive navigation
- [ ] Create reusable component styles
- [ ] Add loading states
- [ ] Add success/error notifications

**Acceptance Criteria:**
- All pages have consistent styling
- UI is mobile-responsive
- Forms are user-friendly

---

### Issue #24: Implement Greek Localization
**Labels:** `frontend`, `enhancement`

**Description:**
Add Greek language support throughout the application.

**Tasks:**
- [ ] Set up localization in Program.cs
- [ ] Create resource files for Greek (el) and English (en)
- [ ] Translate all UI strings
- [ ] Implement language switcher
- [ ] Configure date/time formatting for Greek locale
- [ ] Test all pages in both languages

**Acceptance Criteria:**
- Application supports Greek and English
- All text is translatable
- Date/time formats are locale-appropriate

---

### Issue #25: Add Client-Side Validation
**Labels:** `frontend`, `enhancement`

**Description:**
Enhance forms with client-side validation.

**Tasks:**
- [ ] Add jQuery Validation (if using)
- [ ] Implement validation on all forms
- [ ] Add custom validation rules
- [ ] Display validation errors clearly
- [ ] Add real-time validation feedback
- [ ] Ensure server-side validation still works

**Acceptance Criteria:**
- Forms validate before submission
- Error messages are clear
- Both client and server validation work

---

## Phase 6: Security & Testing

### Issue #26: Implement Authorization Policies
**Labels:** `security`, `backend`, `enhancement`

**Description:**
Create and apply authorization policies for role-based access.

**Tasks:**
- [ ] Create Family and Caregiver roles
- [ ] Implement role-based authorization policies
- [ ] Apply [Authorize] attributes to controllers
- [ ] Restrict actions by user type
- [ ] Add resource-based authorization (users editing own data)
- [ ] Test unauthorized access attempts

**Acceptance Criteria:**
- Users can only access appropriate features
- Role-based restrictions work
- Unauthorized access is properly denied

---

### Issue #27: Add Input Validation & Sanitization
**Labels:** `security`, `backend`, `enhancement`

**Description:**
Ensure all user inputs are validated and sanitized.

**Tasks:**
- [ ] Add data annotations to all models
- [ ] Implement custom validators where needed
- [ ] Sanitize text inputs (prevent XSS)
- [ ] Validate file uploads (size, type)
- [ ] Add anti-forgery tokens to forms
- [ ] Implement rate limiting (optional)

**Acceptance Criteria:**
- All inputs are validated
- XSS attacks are prevented
- File uploads are secure

---

### Issue #28: Write Unit Tests for Services
**Labels:** `testing`, `enhancement`

**Description:**
Create unit tests for service layer.

**Tasks:**
- [ ] Set up xUnit test project
- [ ] Install Moq for mocking
- [ ] Write tests for ProfileService
- [ ] Write tests for BookingService
- [ ] Write tests for ReviewService
- [ ] Write tests for CaregiverSearchService
- [ ] Aim for >70% code coverage

**Acceptance Criteria:**
- All service methods have tests
- Tests pass successfully
- Edge cases are covered

---

### Issue #29: Write Integration Tests
**Labels:** `testing`, `enhancement`

**Description:**
Create integration tests for database operations.

**Tasks:**
- [ ] Set up in-memory database for testing
- [ ] Write tests for repository operations
- [ ] Write tests for DbContext queries
- [ ] Test migrations
- [ ] Test data seeding

**Acceptance Criteria:**
- Database operations are tested
- Tests use isolated in-memory database
- All tests pass

---

## Phase 7: Additional Features

### Issue #30: Implement Email Notifications
**Labels:** `enhancement`, `backend`

**Description:**
Add email notifications for key events.

**Tasks:**
- [ ] Set up email service (SMTP/SendGrid)
- [ ] Create email templates
- [ ] Send welcome email on registration
- [ ] Send booking confirmation emails
- [ ] Send booking reminders
- [ ] Send new message notifications
- [ ] Make email preferences configurable

**Acceptance Criteria:**
- Emails are sent for key events
- Templates are professional
- Users can opt out

---

### Issue #31: Add Database Seeding
**Labels:** `database`, `setup`, `enhancement`

**Description:**
Create seed data for development and testing.

**Tasks:**
- [ ] Create database seeder class
- [ ] Add sample service categories
- [ ] Add sample caregiver profiles
- [ ] Add sample family profiles
- [ ] Add sample bookings
- [ ] Add sample reviews
- [ ] Configure seeding in Program.cs

**Acceptance Criteria:**
- Database can be seeded with sample data
- Data is realistic and useful for testing
- Seeding only runs in development

---

### Issue #32: Implement File Upload for Profile Pictures
**Labels:** `backend`, `frontend`, `enhancement`

**Description:**
Add profile picture upload functionality.

**Tasks:**
- [ ] Configure file storage (local or cloud)
- [ ] Add image validation (size, format)
- [ ] Implement image resizing/optimization
- [ ] Create upload endpoint
- [ ] Add upload UI component
- [ ] Handle file deletion on profile update
- [ ] Add default avatar images

**Acceptance Criteria:**
- Users can upload profile pictures
- Images are validated and optimized
- Storage is properly configured

---

### Issue #33: Add Search Indexing & Optimization
**Labels:** `backend`, `database`, `enhancement`

**Description:**
Optimize database queries and add search indexes.

**Tasks:**
- [ ] Identify slow queries
- [ ] Add indexes on frequently queried columns
- [ ] Optimize LINQ queries
- [ ] Add query result caching where appropriate
- [ ] Test query performance
- [ ] Document optimization decisions

**Acceptance Criteria:**
- Search queries are fast (<200ms)
- Appropriate indexes are in place
- No N+1 query problems

---

### Issue #34: Create Admin Dashboard (Optional)
**Labels:** `enhancement`, `backend`, `frontend`

**Description:**
Build admin panel for managing users and verifications.

**Tasks:**
- [ ] Create Admin role
- [ ] Create AdminController
- [ ] Add user management views
- [ ] Add caregiver verification workflow
- [ ] Add statistics dashboard
- [ ] Add report viewing
- [ ] Restrict access to admin only

**Acceptance Criteria:**
- Admins can verify caregivers
- Admin can view system statistics
- Access is properly restricted

---

### Issue #35: Add Payment Integration (Future)
**Labels:** `enhancement`, `backend`

**Description:**
Integrate payment processing for bookings.

**Tasks:**
- [ ] Research payment providers (Stripe, Viva Wallet)
- [ ] Set up test account
- [ ] Implement payment service
- [ ] Add payment form to booking flow
- [ ] Handle payment success/failure
- [ ] Add transaction history
- [ ] Implement refund logic

**Acceptance Criteria:**
- Payments can be processed
- Transactions are secure
- Refunds work properly

---

## Phase 8: Deployment & DevOps

### Issue #36: Configure Production Environment
**Labels:** `setup`, `deployment`

**Description:**
Prepare application for production deployment.

**Tasks:**
- [ ] Configure production appsettings.json
- [ ] Set up environment variables
- [ ] Configure production database
- [ ] Enable HTTPS
- [ ] Configure logging (Serilog/NLog)
- [ ] Set up error pages
- [ ] Configure CORS if needed

**Acceptance Criteria:**
- Production configuration is secure
- Environment secrets are not in source control
- HTTPS is enforced

---

### Issue #37: Create CI/CD Pipeline
**Labels:** `setup`, `deployment`

**Description:**
Set up automated build and deployment.

**Tasks:**
- [ ] Configure GitHub Actions for CI
- [ ] Add build workflow
- [ ] Add test workflow
- [ ] Add deployment workflow
- [ ] Set up staging environment
- [ ] Configure automatic deployments on merge to main
- [ ] Add rollback capability

**Acceptance Criteria:**
- Code is automatically built and tested
- Deployments are automated
- Pipeline is reliable

---

### Issue #38: Deploy to Azure/AWS (Choose Platform)
**Labels:** `deployment`

**Description:**
Deploy application to cloud hosting.

**Tasks:**
- [ ] Choose hosting platform (Azure App Service, AWS Elastic Beanstalk, etc.)
- [ ] Set up hosting account
- [ ] Configure database (Azure SQL, AWS RDS)
- [ ] Deploy application
- [ ] Configure custom domain
- [ ] Set up SSL certificate
- [ ] Configure backups
- [ ] Set up monitoring

**Acceptance Criteria:**
- Application is live and accessible
- Database is properly configured
- SSL is working

---

## Documentation Tasks

### Issue #39: Create API Documentation
**Labels:** `documentation`

**Description:**
Document all API endpoints (if creating API).

**Tasks:**
- [ ] Install Swashbuckle/NSwag
- [ ] Configure Swagger UI
- [ ] Add XML documentation comments
- [ ] Document all endpoints
- [ ] Add request/response examples
- [ ] Test API documentation

**Acceptance Criteria:**
- All endpoints are documented
- Swagger UI is accessible
- Examples are accurate

---

### Issue #40: Create User Guide
**Labels:** `documentation`

**Description:**
Write user-facing documentation.

**Tasks:**
- [ ] Create user guide for families
- [ ] Create user guide for caregivers
- [ ] Document registration process
- [ ] Document booking process
- [ ] Add screenshots
- [ ] Translate to Greek
- [ ] Add FAQ section

**Acceptance Criteria:**
- User guides are comprehensive
- Both languages are available
- FAQs cover common questions

---

## Getting Started

1. **Create all issues** in your GitHub repository
2. **Assign labels** appropriately
3. **Create milestones** for each phase
4. **Work with Claude** by mentioning tasks in commits: "Fixes #1", "Implements #10"
5. **Use pull requests** with the Claude Actions workflow for code review

## Priority Order
1. Phase 1 (Setup) - Issues #1-3
2. Phase 2 (Models) - Issues #4-9
3. Phase 3 (Services) - Issues #10-15
4. Phase 4 (Controllers) - Issues #16-22
5. Continue with remaining phases

Good luck with your Frontida development! 🏥
