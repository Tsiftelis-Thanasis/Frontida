using System.ComponentModel.DataAnnotations;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class CreatePostViewModel
{
    [Required]
    [StringLength(200, MinimumLength = 5)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 20)]
    [Display(Name = "Description")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Service type")]
    public ServiceType? ServiceType { get; set; }

    [StringLength(100)]
    [Display(Name = "City")]
    public string? City { get; set; }
}
