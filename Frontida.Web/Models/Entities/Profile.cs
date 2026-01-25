using System.ComponentModel.DataAnnotations;

namespace Frontida.Web.Models.Entities;

public class Profile
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public decimal? HourlyRate { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Languages { get; set; }
    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }
    
    public ICollection<Service> Services { get; set; } = new List<Service>();
}
