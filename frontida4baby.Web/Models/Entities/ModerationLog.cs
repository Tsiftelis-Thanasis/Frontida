namespace frontida4baby.Web.Models.Entities;

public class ModerationLog
{
    public int Id { get; set; }

    public ContentType ContentType { get; set; }
    public int ContentId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public ModerationStage Stage { get; set; }
    public ModerationStatus Decision { get; set; }
    public string? Reason { get; set; }
    public string? ViolationCategory { get; set; }
    public float? ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
