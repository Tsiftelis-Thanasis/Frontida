using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class CaregiverProfileViewModel
{
    public string   UserId            { get; set; } = "";
    public string   FullName          { get; set; } = "";
    public string?  City              { get; set; }
    public string?  ProfileImageUrl   { get; set; }
    public string?  Bio               { get; set; }
    public decimal? HourlyRate        { get; set; }
    public int?     YearsOfExperience { get; set; }
    public bool     IsVerified        { get; set; }
    public bool     IsEmailVerified   { get; set; }
    public double   AverageRating     { get; set; }
    public int      ReviewCount       { get; set; }
    public List<ServiceWithDescription> Services { get; set; } = new();

    // Gated by subscription
    public string?  Phone          { get; set; }   // null = not visible
    public bool     IsAuthenticated { get; set; }
    public bool     CanSeeReviews  { get; set; }
    public List<ReviewDetailItem>  Reviews { get; set; } = new();
}

public class ServiceWithDescription
{
    public ServiceType ServiceType  { get; set; }
    public string?     Description  { get; set; }
}

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
