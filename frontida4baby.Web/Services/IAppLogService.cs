using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

public interface IAppLogService
{
    Task LogAsync(AppLogLevel level, string category, string message,
        string? details = null, string? userId = null, string? path = null);
}
