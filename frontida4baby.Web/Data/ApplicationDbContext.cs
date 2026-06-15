using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Profile> Profiles { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Reply> Replies { get; set; }
    public DbSet<ModerationLog> ModerationLogs { get; set; }
    public DbSet<PostReaction> PostReactions { get; set; }
    public DbSet<SavedPost> SavedPosts { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<AppLog> AppLogs { get; set; }
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser → Profile
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<Profile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Booking relationships
        builder.Entity<Booking>()
            .HasOne(b => b.FamilyUser)
            .WithMany(u => u.SentBookings)
            .HasForeignKey(b => b.FamilyUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.CaregiverUser)
            .WithMany(u => u.ReceivedBookings)
            .HasForeignKey(b => b.CaregiverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review relationships
        builder.Entity<Review>()
            .HasOne(r => r.ReviewerUser)
            .WithMany(u => u.WrittenReviews)
            .HasForeignKey(r => r.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.ReviewedUser)
            .WithMany(u => u.ReceivedReviews)
            .HasForeignKey(r => r.ReviewedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Profile → Service
        builder.Entity<Service>()
            .HasOne(s => s.Profile)
            .WithMany(p => p.Services)
            .HasForeignKey(s => s.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Profile>()
            .Property(p => p.HourlyRate)
            .HasPrecision(18, 2);

        // Post relationships
        builder.Entity<Post>()
            .HasOne(p => p.AuthorUser)
            .WithMany(u => u.Posts)
            .HasForeignKey(p => p.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Post>()
            .HasMany(p => p.Replies)
            .WithOne(r => r.Post)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reply → author
        builder.Entity<Reply>()
            .HasOne(r => r.AuthorUser)
            .WithMany(u => u.Replies)
            .HasForeignKey(r => r.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for common queries
        builder.Entity<Post>()
            .HasIndex(p => p.ModerationStatus);
        builder.Entity<Post>()
            .HasIndex(p => p.CreatedAt);
        builder.Entity<Reply>()
            .HasIndex(r => r.PostId);
        builder.Entity<Reply>()
            .HasIndex(r => r.ModerationStatus);
        builder.Entity<Post>()
            .HasIndex(p => new { p.Status, p.ModerationStatus, p.CreatedAt });

        builder.Entity<ModerationLog>()
            .HasIndex(m => new { m.ContentType, m.ContentId });

        // PostReaction
        builder.Entity<PostReaction>()
            .HasOne(r => r.Post)
            .WithMany(p => p.Reactions)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PostReaction>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reactions)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PostReaction>()
            .HasIndex(r => new { r.PostId, r.UserId })
            .IsUnique();

        // SavedPost
        builder.Entity<SavedPost>()
            .HasOne(s => s.Post)
            .WithMany(p => p.SavedBy)
            .HasForeignKey(s => s.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedPost>()
            .HasOne(s => s.User)
            .WithMany(u => u.SavedPosts)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<SavedPost>()
            .HasIndex(s => new { s.PostId, s.UserId })
            .IsUnique();

        // Subscription (one-to-one with ApplicationUser)
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Subscription)
            .WithOne(s => s.User)
            .HasForeignKey<Subscription>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
