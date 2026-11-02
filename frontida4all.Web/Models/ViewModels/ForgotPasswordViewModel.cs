using System.ComponentModel.DataAnnotations;

namespace frontida4all.Web.Models.ViewModels;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
