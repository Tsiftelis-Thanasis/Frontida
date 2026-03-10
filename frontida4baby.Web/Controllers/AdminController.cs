using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET /admin
    public async Task<IActionResult> Index()
    {
        var totalUsers    = await _db.Users.CountAsync();
        var postsToday    = await _db.Posts.CountAsync(p => p.CreatedAt >= DateTime.UtcNow.Date);
        var pendingMod    = await _db.Posts.CountAsync(p => p.ModerationStatus == ModerationStatus.PendingReview)
                         + await _db.Replies.CountAsync(r => r.ModerationStatus == ModerationStatus.PendingReview);
        var errorsRecent  = await _db.AppLogs.CountAsync(l =>
            l.Level == AppLogLevel.Error && l.CreatedAt >= DateTime.UtcNow.AddDays(-1));

        ViewBag.TotalUsers    = totalUsers;
        ViewBag.PostsToday    = postsToday;
        ViewBag.PendingMod    = pendingMod;
        ViewBag.ErrorsRecent  = errorsRecent;
        return View();
    }

    // GET /admin/users
    public async Task<IActionResult> Users(int page = 1)
    {
        const int pageSize = 20;
        var users = await _db.Users
            .Include(u => u.Subscription)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page  = page;
        ViewBag.Total = await _db.Users.CountAsync();
        return View(users);
    }

    // GET /admin/users/{id}
    public async Task<IActionResult> UserDetail(string id)
    {
        var user = await _db.Users
            .Include(u => u.Subscription)
            .Include(u => u.Posts)
            .Include(u => u.Replies).ThenInclude(r => r.Post)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();
        ViewBag.Roles = await _userManager.GetRolesAsync(user);
        return View(user);
    }

    // POST /admin/users/{id}/unblacklist
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblacklistUser(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsBlacklisted   = false;
        user.BlacklistReason = null;
        user.BlacklistedAt   = null;
        await _db.SaveChangesAsync();

        TempData["AdminMsg"] = "Ο αποκλεισμός χρήστη αρθεί επιτυχώς.";
        return RedirectToAction(nameof(UserDetail), new { id });
    }

    // POST /admin/users/{id}/set-plan
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlan(string id, SubscriptionPlan plan)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == id);
        if (sub == null)
        {
            sub = new Subscription { UserId = id, StartDate = DateTime.UtcNow };
            _db.Subscriptions.Add(sub);
        }
        sub.Plan   = plan;
        sub.Status = SubscriptionStatus.Active;
        await _db.SaveChangesAsync();

        TempData["AdminMsg"] = $"Plan updated to {plan}.";
        return RedirectToAction(nameof(UserDetail), new { id });
    }

    // GET /admin/posts
    public async Task<IActionResult> Posts(ModerationStatus? status, int page = 1)
    {
        const int pageSize = 20;
        var query = _db.Posts.Include(p => p.AuthorUser).AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.ModerationStatus == status.Value);

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page           = page;
        ViewBag.Total          = await query.CountAsync();
        ViewBag.StatusFilter   = status;
        return View(posts);
    }

    // GET /admin/logs
    public async Task<IActionResult> Logs(AppLogLevel? level, string? dateFrom, string? dateTo, int page = 1)
    {
        const int pageSize = 50;
        var query = _db.AppLogs.AsQueryable();

        if (level.HasValue)
            query = query.Where(l => l.Level == level.Value);

        if (DateTime.TryParse(dateFrom, out var from))
            query = query.Where(l => l.CreatedAt >= from);

        if (DateTime.TryParse(dateTo, out var to))
            query = query.Where(l => l.CreatedAt <= to.AddDays(1));

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page      = page;
        ViewBag.Total     = await query.CountAsync();
        ViewBag.Level     = level;
        ViewBag.DateFrom  = dateFrom;
        ViewBag.DateTo    = dateTo;
        return View(logs);
    }
}
