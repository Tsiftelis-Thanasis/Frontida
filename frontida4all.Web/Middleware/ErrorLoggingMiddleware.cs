using frontida4all.Web.Services;
using frontida4all.Web.Models.Entities;

namespace frontida4all.Web.Middleware;

public class ErrorLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorLoggingMiddleware> _logger;

    public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context,
        IAppLogService logService,
        INotificationService notifications)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);

            try
            {
                await logService.LogAsync(
                    AppLogLevel.Error,
                    "ErrorMiddleware",
                    ex.Message,
                    details: ex.ToString(),
                    path: context.Request.Path);
            }
            catch { /* don't let logging failure mask the original exception */ }

            try
            {
                await notifications.NotifyServerErrorAsync(context.Request.Path, ex);
            }
            catch { /* best-effort */ }

            throw;
        }
    }
}
