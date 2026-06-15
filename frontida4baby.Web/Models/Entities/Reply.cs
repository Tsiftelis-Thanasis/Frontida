using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.Entities;

public class Reply
{
    public int Id { get; set; }

    [Required]
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser AuthorUser { get; set; } = null!;

    [Required]
    [StringLength(2000, MinimumLength = 5)]
    public string Body { get; set; } = string.Empty;

    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.PendingReview;
    public string? ModerationReason { get; set; }
    public int ModerationAttempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
}
