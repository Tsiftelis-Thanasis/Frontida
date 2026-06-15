using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;

namespace frontida4baby.Web.Controllers;

public class CaregiversController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService         _subscription;

    public CaregiversController(
        ApplicationDbContext         context,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService         subscription)
    {
        _context      = context;
        _userManager  = userManager;
        _subscription = subscription;
    }

    public async Task<IActionResult> Index(CaregiverSearchViewModel searchModel)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.Profile)
                .ThenInclude(p => p!.Services)
            .Include(u => u.ReceivedReviews)
            .AsSplitQuery()
            .Where(u => u.IsCaregiver);

        if (searchModel.ServiceType.HasValue)
        {
            query = query.Where(u => u.Profile!.Services
                .Any(s => s.ServiceType == searchModel.ServiceType.Value && s.IsActive));
        }

        if (!string.IsNullOrEmpty(searchModel.City))
        {
            query = query.Where(u => u.City == searchModel.City);
        }

        if (searchModel.MaxHourlyRate.HasValue)
        {
            query = query.Where(u => u.Profile!.HourlyRate <= searchModel.MaxHourlyRate.Value);
        }

        if (searchModel.VerifiedOnly == true)
        {
            query = query.Where(u => u.Profile!.IsVerified);
        }

        var caregivers = await query
            .Select(u => new CaregiverListItemViewModel
            {
                UserId = u.Id,
                FullName = $"{u.FirstName} {u.LastName}",
                City = u.City,
                ProfileImageUrl = u.Profile != null ? u.Profile.ProfileImageUrl : null,
                HourlyRate = u.Profile != null ? u.Profile.HourlyRate : null,
                YearsOfExperience = u.Profile != null ? u.Profile.YearsOfExperience : null,
                IsVerified = u.Profile != null && u.Profile.IsVerified,
                AverageRating = u.ReceivedReviews.Any() ? u.ReceivedReviews.Average(r => (double)r.Rating) : 0,
                ReviewCount = u.ReceivedReviews.Count,
                Services = u.Profile != null
                    ? u.Profile.Services.Where(s => s.IsActive).Select(s => s.ServiceType).ToList()
                    : new List<frontida4baby.Web.Models.Entities.ServiceType>()
            })
            .ToListAsync();

        searchModel.Caregivers = caregivers;
        return View(searchModel);
    }

    [HttpGet("caregivers/{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var caregiver = await _context.Users
            .Include(u => u.Profile)
                .ThenInclude(p => p!.Services)
            .Include(u => u.ReceivedReviews)
                .ThenInclude(r => r.ReviewerUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsCaregiver);

        if (caregiver is null) return NotFound();

        bool isAuthenticated = User.Identity?.IsAuthenticated == true;
        bool phoneVisible     = false;
        bool canSeeReviews    = false;

        if (isAuthenticated)
        {
            var viewer = await _userManager.GetUserAsync(User);
            if (viewer is not null)
            {
                bool isAdmin = User.IsInRole("Admin");
                phoneVisible  = isAdmin || await _subscription.IsPhoneVisibleAsync(viewer.Id);
                canSeeReviews = isAdmin || await _subscription.GetPlanAsync(viewer.Id) == SubscriptionPlan.Paid;
            }
        }

        var vm = new CaregiverProfileViewModel
        {
            UserId            = caregiver.Id,
            FullName          = $"{caregiver.FirstName} {caregiver.LastName}".Trim(),
            City              = caregiver.City,
            ProfileImageUrl   = caregiver.Profile?.ProfileImageUrl,
            Bio               = caregiver.Profile?.Bio,
            HourlyRate        = caregiver.Profile?.HourlyRate,
            YearsOfExperience = caregiver.Profile?.YearsOfExperience,
            IsVerified        = caregiver.Profile?.IsVerified ?? false,
            IsEmailVerified   = caregiver.EmailConfirmed,
            AverageRating     = caregiver.ReceivedReviews.Any()
                                    ? caregiver.ReceivedReviews.Average(r => (double)r.Rating) : 0,
            ReviewCount       = caregiver.ReceivedReviews.Count,
            Services          = caregiver.Profile?.Services
                                    .Where(s => s.IsActive)
                                    .Select(s => new ServiceWithDescription
                                    {
                                        ServiceType = s.ServiceType,
                                        Description = s.Description
                                    }).ToList() ?? new(),
            Phone             = phoneVisible ? caregiver.PhoneNumber : null,
            IsAuthenticated   = isAuthenticated,
            CanSeeReviews     = canSeeReviews,
            Reviews           = canSeeReviews
                                    ? caregiver.ReceivedReviews
                                        .OrderByDescending(r => r.CreatedAt)
                                        .Select(r => new ReviewDetailItem
                                        {
                                            ReviewerName = $"{r.ReviewerUser.FirstName} {r.ReviewerUser.LastName}".Trim(),
                                            Rating       = r.Rating,
                                            Comment      = r.Comment,
                                            CreatedAt    = r.CreatedAt
                                        }).ToList()
                                    : new()
        };

        return View(vm);
    }
}
