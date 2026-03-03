using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscription;

    public DashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscription)
    {
        _db = db;
        _userManager = userManager;
        _subscription = subscription;
    }

    public async Task<IActionResult> Index(string tab = "posts")
    {
        var userId = _userManager.GetUserId(User)!;
        var plan   = await _subscription.GetPlanAsync(userId);

        var vm = new DashboardViewModel
        {
            CurrentPlan = plan,
            ActiveTab   = tab,
        };

        if (tab == "posts" || tab == "")
        {
            vm.MyPosts = await _db.Posts
                .Where(p => p.AuthorUserId == userId && p.Status != PostStatus.Deleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostListItemViewModel
                {
                    Id          = p.Id,
                    Title       = p.Title,
                    ServiceType = p.ServiceType,
                    City        = p.City,
                    ReplyCount  = p.Replies.Count(r => r.ModerationStatus == ModerationStatus.Approved),
                    CreatedAt   = p.CreatedAt,
                })
                .ToListAsync();
        }
        else if (tab == "replies")
        {
            vm.MyReplies = await _db.Replies
                .Include(r => r.Post)
                .Where(r => r.AuthorUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReplyDashboardItem
                {
                    ReplyId   = r.Id,
                    PostId    = r.PostId,
                    PostTitle = r.Post.Title,
                    Body      = r.Body,
                    CreatedAt = r.CreatedAt,
                    EditedAt  = r.EditedAt,
                })
                .ToListAsync();
        }
        else if (tab == "reactions")
        {
            vm.ReactedPosts = await _db.PostReactions
                .Include(r => r.Post)
                .Where(r => r.UserId == userId && r.Post.Status != PostStatus.Deleted)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new PostListItemViewModel
                {
                    Id          = r.Post.Id,
                    Title       = r.Post.Title,
                    ServiceType = r.Post.ServiceType,
                    City        = r.Post.City,
                    ReplyCount  = r.Post.Replies.Count(rp => rp.ModerationStatus == ModerationStatus.Approved),
                    CreatedAt   = r.Post.CreatedAt,
                })
                .ToListAsync();
        }
        else if (tab == "saved")
        {
            vm.SavedPosts = await _db.SavedPosts
                .Include(s => s.Post)
                .Where(s => s.UserId == userId && s.Post.Status != PostStatus.Deleted)
                .OrderByDescending(s => s.SavedAt)
                .Select(s => new PostListItemViewModel
                {
                    Id          = s.Post.Id,
                    Title       = s.Post.Title,
                    ServiceType = s.Post.ServiceType,
                    City        = s.Post.City,
                    ReplyCount  = s.Post.Replies.Count(r => r.ModerationStatus == ModerationStatus.Approved),
                    CreatedAt   = s.Post.CreatedAt,
                })
                .ToListAsync();
        }

        return View(vm);
    }

    [HttpGet("dashboard/settings")]
    public async Task<IActionResult> Settings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id);

        var vm = new UserSettingsViewModel
        {
            FirstName = user.FirstName,
            LastName  = user.LastName,
            Phone     = user.PhoneNumber,
            City      = user.City,
            Bio       = profile?.Bio,
        };
        return View(vm);
    }

    [HttpPost("dashboard/settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(UserSettingsViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        user.FirstName   = model.FirstName;
        user.LastName    = model.LastName;
        user.PhoneNumber = model.Phone;
        user.City        = model.City;

        await _userManager.UpdateAsync(user);

        // Update profile bio
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile != null)
        {
            profile.Bio = model.Bio;
            await _db.SaveChangesAsync();
        }

        TempData["SettingsSaved"] = true;
        return RedirectToAction(nameof(Settings));
    }
}
