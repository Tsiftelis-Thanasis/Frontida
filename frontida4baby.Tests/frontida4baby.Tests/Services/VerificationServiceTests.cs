using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace frontida4baby.Tests.Services;

public class VerificationServiceTests
{
    private sealed class NoOpUserEmail : IUserEmailService
    {
        public int ReviewEmails;
        public Task SendWelcomeAsync(ApplicationUser user) => Task.CompletedTask;
        public Task SendContentRejectedAsync(ApplicationUser user, string contentSnippet, string reason) => Task.CompletedTask;
        public Task SendReactionApprovedAsync(ApplicationUser caregiver, string postTitle, ApplicationUser op) => Task.CompletedTask;
        public Task SendPaymentSucceededAsync(ApplicationUser user) => Task.CompletedTask;
        public Task SendPaymentFailedAsync(ApplicationUser user) => Task.CompletedTask;
        public Task SendSubscriptionCancelledAsync(ApplicationUser user) => Task.CompletedTask;
        public Task SendSupportConfirmationAsync(string toEmail, string name, string subject) => Task.CompletedTask;
        public Task SendRefundIssuedAsync(ApplicationUser user, decimal amount, string currency) => Task.CompletedTask;
        public Task SendTicketReplyAsync(SupportTicket ticket, string replyBody) => Task.CompletedTask;
        public Task SendVerificationReviewAsync(ApplicationUser user, VerificationDocumentType type, bool approved, string? reason)
        { ReviewEmails++; return Task.CompletedTask; }
        public Task SendVerificationExpiryAsync(ApplicationUser user, VerificationDocumentType type, DateTime expiresOn, bool expired) => Task.CompletedTask;
    }

    private sealed class FakeSms : ISmsSender
    {
        public bool IsAvailable => true;
        public Task<bool> SendAsync(string toE164, string message, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static (ApplicationDbContext Db, VerificationService Svc, NoOpUserEmail Email) Create(VerificationOptions? opts = null)
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ApplicationDbContext(dbOptions);
        var email = new NoOpUserEmail();
        // UserManager is only needed for the phone-code flow, which is not exercised here.
        var svc = new VerificationService(db, null!, new FakeSms(), email,
            new ConfigurationBuilder().Build(), Options.Create(opts ?? new VerificationOptions()),
            NullLogger<VerificationService>.Instance);
        return (db, svc, email);
    }

    private static async Task<ApplicationUser> SeedCaregiverAsync(ApplicationDbContext db, bool email = true, bool phone = true)
    {
        var user = new ApplicationUser
        {
            Id = "cg-1", UserName = "cg@example.com", Email = "cg@example.com", FirstName = "Eleni",
            IsCaregiver = true, EmailConfirmed = email, PhoneNumber = "+306912345678", PhoneNumberConfirmed = phone,
        };
        db.Users.Add(user);
        db.Profiles.Add(new Profile { UserId = user.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private static IFormFile Pdf(int size = 2000)
    {
        var bytes = new byte[size];
        "%PDF-1.4"u8.CopyTo(bytes);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "doc.pdf");
    }

    private static async Task ApproveAllAsync(ApplicationDbContext db, VerificationService svc, string userId)
    {
        (await svc.SubmitDocumentAsync(userId, VerificationDocumentType.AddressProof, Pdf(), null)).Ok.ShouldBeTrue();
        (await svc.SubmitDocumentAsync(userId, VerificationDocumentType.CriminalRecord, Pdf(), DateTime.UtcNow.Date.AddDays(-10))).Ok.ShouldBeTrue();
        foreach (var doc in await db.VerificationDocuments.ToListAsync())
            (await svc.ReviewDocumentAsync(doc.Id, "admin", true, null, null)).Ok.ShouldBeTrue();

        db.UserVerifications.Single(v => v.UserId == userId).IdentityStatus = IdentityCheckStatus.Verified;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task NewCaregiver_IsNotFullyVerified_AndListsMissingSteps()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);

        var summary = await svc.GetSummaryAsync(user.Id);

        Assert.False(summary.IsFullyVerified);
        Assert.Equal(2, summary.CompletedSteps);   // email + phone only
        Assert.Contains("Έλεγχος ταυτότητας", summary.Missing());
        Assert.Contains("Πιστοποιητικό ποινικού μητρώου", summary.Missing());
    }

    [Fact]
    public async Task AllStepsDone_ProfileGetsVerifiedStamp()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);

        await ApproveAllAsync(db, svc, user.Id);
        await svc.SyncProfileAsync(user.Id);

        var summary = await svc.GetSummaryAsync(user.Id);
        Assert.True(summary.IsFullyVerified);
        Assert.True((await db.Profiles.SingleAsync()).IsVerified);
    }

    [Fact]
    public async Task ExpiredCriminalRecord_RemovesStamp()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);
        await ApproveAllAsync(db, svc, user.Id);
        await svc.SyncProfileAsync(user.Id);

        var v = await db.UserVerifications.SingleAsync();
        v.CriminalRecordExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        await svc.SyncProfileAsync(user.Id);

        var summary = await svc.GetSummaryAsync(user.Id);
        Assert.False(summary.IsFullyVerified);
        Assert.True(summary.CriminalRecordExpired);
        Assert.Contains("Πιστοποιητικό ποινικού μητρώου (έληξε)", summary.Missing());
        Assert.False((await db.Profiles.SingleAsync()).IsVerified);
    }

    [Fact]
    public async Task UnconfirmedEmail_BlocksFullVerification()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db, email: false);

        await ApproveAllAsync(db, svc, user.Id);

        Assert.False((await svc.GetSummaryAsync(user.Id)).IsFullyVerified);
    }

    [Fact]
    public async Task Review_Approved_PurgesFileByDefault_ButKeepsOutcome()
    {
        var (db, svc, email) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);
        await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.CriminalRecord, Pdf(), DateTime.UtcNow.Date.AddDays(-5));
        var doc = await db.VerificationDocuments.SingleAsync();

        var result = await svc.ReviewDocumentAsync(doc.Id, "admin", true, null, null);

        Assert.True(result.Ok);
        doc = await db.VerificationDocuments.SingleAsync();
        Assert.Null(doc.Data);
        Assert.NotNull(doc.DataPurgedAt);
        Assert.Equal(DocumentReviewStatus.Approved, doc.Status);
        Assert.Equal(1, email.ReviewEmails);
        var v = await db.UserVerifications.SingleAsync();
        Assert.NotNull(v.CriminalRecordExpiresAt);
    }

    [Fact]
    public async Task Review_Approved_KeepsFile_WhenRetentionEnabled()
    {
        var (db, svc, _) = Create(new VerificationOptions { RetainDocuments = true });
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);
        await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, Pdf(), null);
        var doc = await db.VerificationDocuments.SingleAsync();

        await svc.ReviewDocumentAsync(doc.Id, "admin", true, null, null);

        Assert.NotNull((await db.VerificationDocuments.SingleAsync()).Data);
    }

    [Fact]
    public async Task Review_Rejected_RequiresReason_AndShowsReasonToUser()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);
        await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, Pdf(), null);
        var doc = await db.VerificationDocuments.SingleAsync();

        Assert.False((await svc.ReviewDocumentAsync(doc.Id, "admin", false, null, "  ")).Ok);
        Assert.True((await svc.ReviewDocumentAsync(doc.Id, "admin", false, null, "Δυσανάγνωστο")).Ok);

        var summary = await svc.GetSummaryAsync(user.Id);
        Assert.Equal("Δυσανάγνωστο", summary.AddressRejectionReason);
        Assert.False(summary.AddressApproved);
    }

    [Fact]
    public async Task Review_CannotApproveAlreadyExpiredCertificate()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);
        await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.CriminalRecord, Pdf(), DateTime.UtcNow.Date.AddDays(-5));
        var doc = await db.VerificationDocuments.SingleAsync();

        // Reviewer corrects the issue date to something older than the validity window.
        var result = await svc.ReviewDocumentAsync(doc.Id, "admin", true, DateTime.UtcNow.Date.AddMonths(-12), null);

        Assert.False(result.Ok);
        Assert.Equal(DocumentReviewStatus.Pending, (await db.VerificationDocuments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Submit_RejectsOldCertificate_WrongContent_AndOversizedFile()
    {
        var (db, svc, _) = Create(new VerificationOptions { MaxFileSizeMb = 1 });
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);

        // Issued 8 months ago, default validity is 6 months
        Assert.False((await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.CriminalRecord, Pdf(),
            DateTime.UtcNow.Date.AddMonths(-8))).Ok);
        // Missing issue date
        Assert.False((await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.CriminalRecord, Pdf(), null)).Ok);
        // Future date
        Assert.False((await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.CriminalRecord, Pdf(),
            DateTime.UtcNow.Date.AddDays(2))).Ok);
        // Fake pdf (wrong magic bytes)
        var fake = new FormFile(new MemoryStream(new byte[2000]), 0, 2000, "file", "doc.pdf");
        Assert.False((await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, fake, null)).Ok);
        // Too large
        Assert.False((await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, Pdf(2 * 1024 * 1024), null)).Ok);
        // Disallowed extension
        var exe = new FormFile(new MemoryStream(new byte[2000]), 0, 2000, "file", "doc.exe");
        Assert.False((await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, exe, null)).Ok);

        Assert.Empty(db.VerificationDocuments);
    }

    [Fact]
    public async Task NewUpload_ReplacesEarlierPendingDocument()
    {
        var (db, svc, _) = Create();
        await using var _db = db;
        var user = await SeedCaregiverAsync(db);

        await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, Pdf(), null);
        await svc.SubmitDocumentAsync(user.Id, VerificationDocumentType.AddressProof, Pdf(3000), null);

        Assert.Equal(1, await db.VerificationDocuments.CountAsync());
        Assert.Equal(3000, (await db.VerificationDocuments.SingleAsync()).FileSize);
    }

    [Theory]
    [InlineData("6912345678", "+306912345678")]
    [InlineData("691 234 5678", "+306912345678")]
    [InlineData("0030 691 2345678", "+306912345678")]
    [InlineData("+30 691 2345678", "+306912345678")]
    [InlineData("+44 7700 900123", "+447700900123")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void NormalizePhone_HandlesCommonFormats(string input, string? expected)
        => Assert.Equal(expected, VerificationService.NormalizePhone(input));

    private static class ShouldBeExtensions { }
}

internal static class BoolAssertExtensions
{
    public static void ShouldBeTrue(this bool value) => Assert.True(value);
}

public class VerificationDateKindTests
{
    [Fact]
    public async Task IssuedOn_IsStoredAsUtc_EvenWhenBoundAsUnspecified()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(dbOptions);
        db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1", IsCaregiver = true });
        await db.SaveChangesAsync();
        var svc = new VerificationService(db, null!, null!, null!, new ConfigurationBuilder().Build(),
            Options.Create(new VerificationOptions()), NullLogger<VerificationService>.Instance);

        var bytes = new byte[100];
        "%PDF-1.4"u8.CopyTo(bytes);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "a.pdf");
        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-3), DateTimeKind.Unspecified);

        Assert.True((await svc.SubmitDocumentAsync("u1", VerificationDocumentType.CriminalRecord, file, unspecified)).Ok);

        Assert.Equal(DateTimeKind.Utc, (await db.VerificationDocuments.SingleAsync()).IssuedOn!.Value.Kind);
    }
}
