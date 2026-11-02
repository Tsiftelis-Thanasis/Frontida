using frontida4all.Web.Models.Entities;

namespace frontida4all.Web.Services;

public interface IAppLogService
{
    Task LogAsync(AppLogLevel level, string category, string message,
        string? details = null, string? userId = null, string? path = null);
}
