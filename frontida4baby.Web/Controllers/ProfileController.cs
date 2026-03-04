using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;

namespace frontida4baby.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext        _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService        _subscription;

    public ProfileController(
        ApplicationDbContext        db,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService        subscription)
    {
        _db           = db;
        _userManager  = userManager;
        _subscription = subscription;
    }

    // GET /profile/{userId}
    [HttpGet("profile/{userId}")]
    public async Task<IActionResult> Details(string userId)
    {
        var viewer = await _userManager.GetUserAsync(User);
        if (viewer is null || !viewer.IsCaregiver)
            return Forbid();

        var canView = await _subscription.CanViewProfileAsync(viewer.Id, userId);
        if (!canView)
        {
            TempData["ProfileError"] = "React to one of their posts to unlock their profile.";
            return RedirectToAction("Index", "Posts");
        }

        var profileUser = await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Posts)
            .Include(u => u.ReceivedReviews)
                .ThenInclude(r => r.ReviewerUser)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (profileUser is null) return NotFound();

        var recentPosts = profileUser.Posts
            .Where(p => p.ModerationStatus == ModerationStatus.Approved && p.Status == PostStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new PostListItemViewModel
            {
                Id          = p.Id,
                Title       = p.Title,
                AuthorName  = $"{profileUser.FirstName} {profileUser.LastName}".Trim(),
                ServiceType = p.ServiceType,
                City        = p.City,
                CreatedAt   = p.CreatedAt,
            })
            .ToList();

        bool isAdmin = User.IsInRole("Admin");
        bool isPaid  = !isAdmin &&
            await _db.Subscriptions.AnyAsync(s =>
                s.UserId == viewer.Id &&
                s.Plan   == SubscriptionPlan.Paid &&
                s.Status == SubscriptionStatus.Active);
        bool canSeeRatingDetails = isAdmin || isPaid;

        var reviews = canSeeRatingDetails
            ? profileUser.ReceivedReviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDetailItem
                {
                    ReviewerName = $"{r.ReviewerUser.FirstName} {r.ReviewerUser.LastName}".Trim(),
                    Rating       = r.Rating,
                    Comment      = r.Comment,
                    CreatedAt    = r.CreatedAt,
                }).ToList()
            : new List<ReviewDetailItem>();

        var vm = new PublicProfileViewModel
        {
            UserId              = profileUser.Id,
            FullName            = $"{profileUser.FirstName} {profileUser.LastName}".Trim(),
            City                = profileUser.City,
            IsCaregiver         = profileUser.IsCaregiver,
            Bio                 = profileUser.Profile?.Bio,
            Phone               = profileUser.PhoneNumber,
            PhoneVisible        = true,
            AverageRating       = profileUser.ReceivedReviews.Any() ? profileUser.ReceivedReviews.Average(r => (double)r.Rating) : 0,
            ReviewCount         = profileUser.ReceivedReviews.Count,
            CanSeeRatingDetails = canSeeRatingDetails,
            Reviews             = reviews,
            RecentPosts         = recentPosts,
        };

        return View(vm);
    }
}
