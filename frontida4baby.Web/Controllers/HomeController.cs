using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;

namespace frontida4baby.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IUserEmailService    _userEmail;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        ApplicationDbContext db,
        INotificationService notifications,
        IUserEmailService userEmail,
        UserManager<ApplicationUser> userManager)
    {
        _db            = db;
        _notifications = notifications;
        _userEmail     = userEmail;
        _userManager   = userManager;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.CaregiverCount = await _db.Users.CountAsync(u => u.IsCaregiver);
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Route("disclaimer")]
    public IActionResult Disclaimer()
    {
        return View();
    }

    [Route("terms")]
    public IActionResult TermsOfService()
    {
        return View();
    }

    [Route("ai-review-policy")]
    public IActionResult AiReviewPolicy()
    {
        return View();
    }

    [Route("contact")]
    [HttpGet]
    public IActionResult Support()
    {
        var model = new SupportViewModel();
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            if (user is not null)
            {
                model.Name  = $"{user.FirstName} {user.LastName}".Trim();
                model.Email = user.Email ?? "";
            }
        }
        return View(model);
    }

    [Route("contact")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Support(SupportViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var categoryLabel = model.Category switch
        {
            SupportCategory.Technical => "Τεχνικό",
            SupportCategory.Billing   => "Χρεώσεις",
            SupportCategory.Safety    => "Ασφάλεια",
            _                         => "Γενικό"
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await _notifications.NotifySupportRequestAsync(
                    model.Name, model.Email, model.Subject, categoryLabel, model.Message);
            }
            catch { /* best-effort */ }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await _userEmail.SendSupportConfirmationAsync(
                    model.Email, model.Name, model.Subject);
            }
            catch { /* best-effort */ }
        });

        TempData["SupportSent"] = true;
        return RedirectToAction(nameof(Support));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
