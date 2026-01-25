using Frontida.Web.Models.Entities;

namespace Frontida.Web.Models.ViewModels;

public class CaregiverSearchViewModel
{
    public ServiceType? ServiceType { get; set; }
    public string? City { get; set; }
    public decimal? MaxHourlyRate { get; set; }
    public bool? VerifiedOnly { get; set; }
    
    public List<CaregiverListItemViewModel> Caregivers { get; set; } = new();
}

public class CaregiverListItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? ProfileImageUrl { get; set; }
    public decimal? HourlyRate { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool IsVerified { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<ServiceType> Services { get; set; } = new();
}
