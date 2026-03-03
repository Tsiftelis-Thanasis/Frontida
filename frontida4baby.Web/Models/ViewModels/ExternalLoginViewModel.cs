using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.ViewModels;

public class ExternalLoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
