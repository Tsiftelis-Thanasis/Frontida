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
    private readonly IWebHostEnvironment _env;

    public DashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscription,
        IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _subscription = subscription;
        _env = env;
    }

    public async Task<IActionResult> Index(string tab = "posts")
    {
        var userId = _userManager.GetUserId(User)!;
        var plan   = await _subscription.GetPlanAsync(userId);
        var user   = await _userManager.FindByIdAsync(userId);

        var isCaregiver = user?.IsCaregiver ?? false;
        var hasServices = isCaregiver && await _db.Profiles
            .Where(p => p.UserId == userId)
            .SelectMany(p => p.Services)
            .AnyAsync(s => s.IsActive);

        var vm = new DashboardViewModel
        {
            CurrentPlan     = plan,
            ActiveTab       = tab,
            IsEmailVerified = user?.EmailConfirmed ?? false,
            IsCaregiver     = isCaregiver,
            HasServices     = hasServices,
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

        var profile = await _db.Profiles
            .Include(p => p.Services)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        var vm = new UserSettingsViewModel
        {
            FirstName         = user.FirstName,
            LastName          = user.LastName,
            Phone             = user.PhoneNumber,
            City              = user.City,
            Bio               = profile?.Bio,
            IsCaregiver       = user.IsCaregiver,
            ProfileImageUrl   = profile?.ProfileImageUrl,
            HourlyRate        = profile?.HourlyRate,
            YearsOfExperience = profile?.YearsOfExperience,
            Languages         = profile?.Languages,
            SelectedServiceTypes = profile?.Services
                .Where(s => s.IsActive)
                .Select(s => s.ServiceType)
                .ToList() ?? new(),
        };
        return View(vm);
    }

    [HttpPost("dashboard/settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(UserSettingsViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        model.IsCaregiver = user.IsCaregiver;

        if (user.IsCaregiver && (model.SelectedServiceTypes == null || model.SelectedServiceTypes.Count == 0))
        {
            ModelState.AddModelError(nameof(model.SelectedServiceTypes),
                "Επιλέξτε τουλάχιστον μία υπηρεσία.");
        }

        if (!ModelState.IsValid)
            return View(model);

        user.FirstName   = model.FirstName;
        user.LastName    = model.LastName;
        user.PhoneNumber = model.Phone;
        user.City        = model.City;
        await _userManager.UpdateAsync(user);

        // Upsert profile
        var profile = await _db.Profiles
            .Include(p => p.Services)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile == null)
        {
            profile = new Profile { UserId = user.Id };
            _db.Profiles.Add(profile);
        }

        profile.Bio = model.Bio;

        if (user.IsCaregiver)
        {
            profile.HourlyRate        = model.HourlyRate;
            profile.YearsOfExperience = model.YearsOfExperience;
            profile.Languages         = model.Languages;

            // Sync service types
            var existing = profile.Services.ToList();
            var selected = model.SelectedServiceTypes ?? new();

            // Remove deselected
            foreach (var svc in existing.Where(s => !selected.Contains(s.ServiceType)))
                _db.Remove(svc);

            // Add newly selected
            foreach (var st in selected.Where(t => !existing.Any(s => s.ServiceType == t)))
                profile.Services.Add(new Service { ServiceType = st, IsActive = true });
        }

        // Handle photo upload
        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.ProfileImage),
                    "Μόνο εικόνες JPG, PNG ή WEBP γίνονται αποδεκτές.");
                model.IsCaregiver     = user.IsCaregiver;
                model.ProfileImageUrl = profile.ProfileImageUrl;
                return View(model);
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadsDir);

            // Delete old file if it was one we stored
            if (!string.IsNullOrEmpty(profile.ProfileImageUrl)
                && profile.ProfileImageUrl.StartsWith("/uploads/profiles/"))
            {
                var oldPath = Path.Combine(_env.WebRootPath,
                    profile.ProfileImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            var fileName = $"{user.Id}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await model.ProfileImage.CopyToAsync(stream);

            profile.ProfileImageUrl = $"/uploads/profiles/{fileName}";
        }

        await _db.SaveChangesAsync();

        TempData["SettingsSaved"] = true;
        return RedirectToAction(nameof(Settings));
    }
}
