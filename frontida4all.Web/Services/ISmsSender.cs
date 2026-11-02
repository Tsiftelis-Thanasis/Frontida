namespace frontida4all.Web.Services;

public interface ISmsSender
{
    /// <summary>True when an SMS provider is configured (or the app runs in Development and logs messages).</summary>
    bool IsAvailable { get; }

    Task<bool> SendAsync(string toE164, string message, CancellationToken ct = default);
}
