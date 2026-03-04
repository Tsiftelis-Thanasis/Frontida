using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.ViewModels;

namespace frontida4baby.Web.Controllers;

public class CaregiversController : Controller
{
    private readonly ApplicationDbContext _context;

    public CaregiversController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CaregiverSearchViewModel searchModel)
    {
        var query = _context.Users
            .Include(u => u.Profile)
                .ThenInclude(p => p!.Services)
            .Include(u => u.ReceivedReviews)
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
                AverageRating = u.ReceivedReviews.Any() ? u.ReceivedReviews.Average(r => r.Rating) : 0,
                ReviewCount = u.ReceivedReviews.Count,
                Services = u.Profile != null
                    ? u.Profile.Services.Where(s => s.IsActive).Select(s => s.ServiceType).ToList()
                    : new List<frontida4baby.Web.Models.Entities.ServiceType>()
            })
            .ToListAsync();

        searchModel.Caregivers = caregivers;
        return View(searchModel);
    }
}
