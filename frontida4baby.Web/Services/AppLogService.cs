using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

public class AppLogService : IAppLogService
{
    private readonly ApplicationDbContext _db;

    public AppLogService(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(AppLogLevel level, string category, string message,
        string? details = null, string? userId = null, string? path = null)
    {
        _db.AppLogs.Add(new AppLog
        {
            Level       = level,
            Category    = category,
            Message     = message?.Length > 500 ? message[..500] : message,
            Details     = details?.Length > 4000 ? details[..4000] : details,
            UserId      = userId,
            RequestPath = path?.Length > 500 ? path[..500] : path,
            CreatedAt   = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
