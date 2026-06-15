namespace frontida4baby.Web.Models.Entities;

public class Subscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(255)]
    public string? StripeCustomerId { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(255)]
    public string? StripeSubscriptionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
