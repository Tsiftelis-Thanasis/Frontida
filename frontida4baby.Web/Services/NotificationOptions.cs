namespace frontida4baby.Web.Services;

public class NotificationOptions
{
    /// <summary>Send email on unhandled 500 exceptions.</summary>
    public bool ServerErrors    { get; set; } = true;

    /// <summary>Send email when a new user registers.</summary>
    public bool NewRegistration { get; set; } = true;

    /// <summary>Send email when a user is auto-blacklisted.</summary>
    public bool UserBlacklisted { get; set; } = true;

    /// <summary>Send email when a post/reply is rejected by moderation.</summary>
    public bool PostRejected    { get; set; } = false;

    /// <summary>Send email to admin when a support request is submitted.</summary>
    public bool SupportRequest  { get; set; } = true;
}
