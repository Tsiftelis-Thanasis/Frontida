using System.ComponentModel.DataAnnotations;

namespace frontida4all.Web.Models.Entities;

public class ProcessedStripeEvent
{
    [Key]
    [StringLength(255)]
    public string EventId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
