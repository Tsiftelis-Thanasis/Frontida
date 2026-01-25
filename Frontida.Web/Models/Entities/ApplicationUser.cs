using Microsoft.AspNetCore.Identity;

namespace Frontida.Web.Models.Entities;

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
    
    public Profile? Profile { get; set; }
    public ICollection<Booking> SentBookings { get; set; } = new List<Booking>();
    public ICollection<Booking> ReceivedBookings { get; set; } = new List<Booking>();
    public ICollection<Review> WrittenReviews { get; set; } = new List<Review>();
    public ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
}
