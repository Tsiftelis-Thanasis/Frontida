using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Frontida.Web.Data;
using Frontida.Web.Models.ViewModels;

namespace Frontida.Web.Controllers;

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
                ProfileImageUrl = u.Profile!.ProfileImageUrl,
                HourlyRate = u.Profile.HourlyRate,
                YearsOfExperience = u.Profile.YearsOfExperience,
                IsVerified = u.Profile.IsVerified,
                AverageRating = u.ReceivedReviews.Any() ? u.ReceivedReviews.Average(r => r.Rating) : 0,
                ReviewCount = u.ReceivedReviews.Count,
                Services = u.Profile.Services.Where(s => s.IsActive).Select(s => s.ServiceType).ToList()
            })
            .ToListAsync();

        searchModel.Caregivers = caregivers;
        return View(searchModel);
    }
}
