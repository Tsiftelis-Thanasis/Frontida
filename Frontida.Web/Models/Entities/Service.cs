using System.ComponentModel.DataAnnotations;

namespace Frontida.Web.Models.Entities;

public class Service
{
    public int Id { get; set; }
    
    [Required]
    public int ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    
    [Required]
    public ServiceType ServiceType { get; set; }
    
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum ServiceType
{
    Childcare,
    ElderlyCare,
    Tutoring,
    Housekeeping,
    PetCare
}
