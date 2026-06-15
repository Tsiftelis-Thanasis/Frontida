using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.ViewModels;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
