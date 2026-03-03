using System.ComponentModel.DataAnnotations;

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
}
