using System.ComponentModel.DataAnnotations;

namespace frontida4baby.Web.Models.Entities;

public enum SupportCategory { General, Technical, Billing, Safety }
public enum SupportTicketStatus { Open, Answered, Closed }

public class SupportTicket
{
    public int Id { get; set; }

    /// <summary>Null for tickets submitted while logged out.</summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    public SupportCategory Category { get; set; } = SupportCategory.General;

    [Required, StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SupportTicketReply> Replies { get; set; } = new List<SupportTicketReply>();
}

public class SupportTicketReply
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;

    [Required, StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    [Required]
    public string RepliedByUserId { get; set; } = string.Empty;
    public ApplicationUser RepliedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
