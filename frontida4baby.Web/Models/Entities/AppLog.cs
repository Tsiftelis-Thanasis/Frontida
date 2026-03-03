namespace frontida4baby.Web.Models.Entities;

public class AppLog
{
    public int Id { get; set; }
    public AppLogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public string? UserId { get; set; }
    public string? RequestPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
