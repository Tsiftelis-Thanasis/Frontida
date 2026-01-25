using System.ComponentModel.DataAnnotations;

namespace Frontida.Web.Models.Entities;

public class Review
{
    public int Id { get; set; }
    
    [Required]
    public string ReviewerUserId { get; set; } = string.Empty;
    public ApplicationUser ReviewerUser { get; set; } = null!;
    
    [Required]
    public string ReviewedUserId { get; set; } = string.Empty;
    public ApplicationUser ReviewedUser { get; set; } = null!;
    
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
    
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }
    
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
