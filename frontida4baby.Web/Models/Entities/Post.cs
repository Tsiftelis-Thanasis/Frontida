using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.Entities;

public class Post
{
    public int Id { get; set; }

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser AuthorUser { get; set; } = null!;

    [Required]
    [StringLength(200, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 20)]
    public string Body { get; set; } = string.Empty;

    public ServiceType? ServiceType { get; set; }
    public string? City { get; set; }

    public PostStatus Status { get; set; } = PostStatus.Active;
    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.PendingReview;
    public string? ModerationReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public ICollection<Reply> Replies { get; set; } = new List<Reply>();
    public ICollection<PostReaction> Reactions { get; set; } = new List<PostReaction>();
    public ICollection<SavedPost> SavedBy { get; set; } = new List<SavedPost>();
}
