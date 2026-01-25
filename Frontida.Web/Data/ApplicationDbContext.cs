using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Frontida.Web.Models.Entities;

namespace Frontida.Web.Data;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure ApplicationUser relationships
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<Profile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Booking relationships
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

        // Configure Review relationships
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

        // Configure Profile-Service relationship
        builder.Entity<Service>()
            .HasOne(s => s.Profile)
            .WithMany(p => p.Services)
            .HasForeignKey(s => s.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure decimal precision for HourlyRate
        builder.Entity<Profile>()
            .Property(p => p.HourlyRate)
            .HasPrecision(18, 2);
    }
}
