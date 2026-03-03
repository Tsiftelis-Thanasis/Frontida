namespace frontida4baby.Web.Services;

public interface IContentModerationService
{
    /// <summary>
    /// Moderates user-submitted content.
    /// Pass <c>null</c> for <paramref name="title"/> when moderating a reply.
    /// </summary>
    Task<ModerationResult> ModerateAsync(string? title, string body);
}
