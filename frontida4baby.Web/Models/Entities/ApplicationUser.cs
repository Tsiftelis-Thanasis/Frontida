using Microsoft.AspNetCore.Identity;

namespace frontida4baby.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public bool IsCaregiver { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Blacklist
    public bool IsBlacklisted { get; set; } = false;
    public string? BlacklistReason { get; set; }
    public DateTime? BlacklistedAt { get; set; }

    // Terms acceptance
    public bool HasAcceptedTerms { get; set; } = false;
    public DateTime? TermsAcceptedAt { get; set; }
    
    public Profile? Profile { get; set; }
    public Subscription? Subscription { get; set; }
    public ICollection<Booking> SentBookings { get; set; } = new List<Booking>();
    public ICollection<Booking> ReceivedBookings { get; set; } = new List<Booking>();
    public ICollection<Review> WrittenReviews { get; set; } = new List<Review>();
    public ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Reply> Replies { get; set; } = new List<Reply>();
    public ICollection<PostReaction> Reactions { get; set; } = new List<PostReaction>();
    public ICollection<SavedPost> SavedPosts { get; set; } = new List<SavedPost>();
}
