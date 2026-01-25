using System.ComponentModel.DataAnnotations;

namespace Frontida.Web.Models.Entities;

public class Booking
{
    public int Id { get; set; }
    
    [Required]
    public string FamilyUserId { get; set; } = string.Empty;
    public ApplicationUser FamilyUser { get; set; } = null!;
    
    [Required]
    public string CaregiverUserId { get; set; } = string.Empty;
    public ApplicationUser CaregiverUser { get; set; } = null!;
    
    [Required]
    public DateTime StartDateTime { get; set; }
    
    [Required]
    public DateTime EndDateTime { get; set; }
    
    public string? Notes { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}
