using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models;

namespace frontida4baby.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
