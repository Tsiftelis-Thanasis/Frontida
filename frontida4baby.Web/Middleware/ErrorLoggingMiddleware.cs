using frontida4baby.Web.Services;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Middleware;

public class ErrorLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorLoggingMiddleware> _logger;

    public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAppLogService logService, IEmailService emailService)
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
            catch
            {
                // Don't let logging failure mask the original exception
            }

            try
            {
                var adminEmail = context.RequestServices
                    .GetRequiredService<IConfiguration>()["Email:AdminEmail"];
                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    await emailService.SendAsync(
                        adminEmail,
                        $"[frontida4baby] Error: {ex.Message}",
                        $"<h3>Unhandled Exception</h3><pre>{System.Net.WebUtility.HtmlEncode(ex.ToString())}</pre><p>Path: {context.Request.Path}</p>");
                }
            }
            catch
            {
                // Best-effort
            }

            throw;
        }
    }
}
