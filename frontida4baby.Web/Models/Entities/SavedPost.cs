namespace frontida4baby.Web.Models.Entities;

public class SavedPost
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
