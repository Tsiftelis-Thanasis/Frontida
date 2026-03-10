namespace frontida4baby.Web.Models.Entities;

public class PostReaction
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Approval by post OP
    public bool IsApprovedByOP { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
}
