using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace frontida4baby.Web.Models.ViewModels;

public class UserSettingsViewModel
{
    [Display(Name = "Όνομα")]
    public string? FirstName { get; set; }

    [Display(Name = "Επώνυμο")]
    public string? LastName { get; set; }

    [Display(Name = "Τηλέφωνο")]
    public string? Phone { get; set; }

    [Display(Name = "Πόλη")]
    public string? City { get; set; }

    [Display(Name = "Βιογραφικό")]
    [StringLength(500)]
    public string? Bio { get; set; }

    // Read-only display fields
    public bool    IsCaregiver     { get; set; }
    public string? ProfileImageUrl { get; set; }

    // Photo upload
    [Display(Name = "Φωτογραφία προφίλ")]
    public IFormFile? ProfileImage { get; set; }

    // Caregiver-specific fields
    [Display(Name = "Ωριαία αμοιβή (€)")]
    [Range(0, 1000)]
    public decimal? HourlyRate { get; set; }

    [Display(Name = "Χρόνια εμπειρίας")]
    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    [Display(Name = "Γλώσσες")]
    [StringLength(200)]
    public string? Languages { get; set; }
}
