namespace frontida4baby.Web.Services;

public class ModerationOptions
{
    public string ClaudeApiKey      { get; set; } = string.Empty;
    public string ClaudeModel       { get; set; } = "claude-haiku-4-5-20251001";
    public int    MaxTokens         { get; set; } = 256;
    public int    TimeoutSeconds    { get; set; } = 10;
    // "Approved" | "Rejected" | "PendingReview"
    public string FallbackOnTimeout  { get; set; } = "PendingReview";
    public int    BlacklistThreshold { get; set; } = 3;
}
