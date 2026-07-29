using System.ComponentModel.DataAnnotations;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class SupportViewModel
{
    [Required(ErrorMessage = "Το όνομα είναι υποχρεωτικό.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Το email είναι υποχρεωτικό.")]
    [EmailAddress(ErrorMessage = "Μη έγκυρη διεύθυνση email.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Το θέμα είναι υποχρεωτικό.")]
    public string Subject { get; set; } = "";

    public SupportCategory Category { get; set; } = SupportCategory.General;

    [Required(ErrorMessage = "Το μήνυμα είναι υποχρεωτικό.")]
    [MaxLength(2000, ErrorMessage = "Το μήνυμα δεν μπορεί να υπερβαίνει τους 2000 χαρακτήρες.")]
    public string Message { get; set; } = "";
}
