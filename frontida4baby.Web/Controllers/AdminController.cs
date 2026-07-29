using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using EntitySubscription = frontida4baby.Web.Models.Entities.Subscription;

namespace frontida4baby.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
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
        var openTickets   = await _db.SupportTickets.CountAsync(t => t.Status == SupportTicketStatus.Open);

        ViewBag.TotalUsers    = totalUsers;
        ViewBag.PostsToday    = postsToday;
        ViewBag.PendingMod    = pendingMod;
        ViewBag.ErrorsRecent  = errorsRecent;
        ViewBag.OpenTickets   = openTickets;
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
            sub = new EntitySubscription { UserId = id, StartDate = DateTime.UtcNow };
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

    // GET /admin/payments
    public async Task<IActionResult> Payments()
    {
        if (!_config.GetValue<bool>("Stripe:Enabled"))
        {
            ViewBag.StripeDisabled = true;
            return View(new List<PaymentViewModel>());
        }

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

        // Map StripeCustomerId → user info from DB
        var subscriptions = await _db.Subscriptions
            .Include(s => s.User)
            .Where(s => s.StripeCustomerId != null)
            .ToListAsync();

        var customerUserMap = subscriptions
            .GroupBy(s => s.StripeCustomerId!)
            .ToDictionary(g => g.Key, g => g.First().User);

        var invoiceOptions = new InvoiceListOptions
        {
            Limit = 100,
        };
        var invoiceService = new InvoiceService();
        var invoices = await invoiceService.ListAsync(invoiceOptions);

        var payments = invoices.Data
            .Where(i => i.Status == "paid")
            .Select(i =>
            {
                customerUserMap.TryGetValue(i.CustomerId, out var user);
                return new PaymentViewModel
                {
                    InvoiceId        = i.Id,
                    StripeCustomerId = i.CustomerId,
                    UserEmail        = user?.Email ?? i.CustomerEmail ?? "–",
                    UserName         = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "–",
                    Amount           = i.AmountPaid / 100m,
                    Currency         = i.Currency.ToUpperInvariant(),
                    Status           = i.Status,
                    PaidAt           = i.StatusTransitions?.PaidAt ?? i.Created,
                    InvoicePdfUrl    = i.InvoicePdf,
                    HostedInvoiceUrl = i.HostedInvoiceUrl,
                    ReceiptUrl       = null, // Stripe.net 50.x: charge receipt not directly accessible on Invoice
                };
            })
            .OrderByDescending(p => p.PaidAt)
            .ToList();

        return View(payments);
    }

    // GET /admin/payments/invoice/{id}  — redirects to Stripe-hosted invoice PDF
    [HttpGet("/admin/payments/invoice/{id}")]
    public async Task<IActionResult> DownloadInvoice(string id)
    {
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var invoice = await new InvoiceService().GetAsync(id);
        if (string.IsNullOrEmpty(invoice?.InvoicePdf)) return NotFound();
        return Redirect(invoice.InvoicePdf);
    }

    // GET /admin/payments/receipt/{id}  — redirects to Stripe charge receipt
    [HttpGet("/admin/payments/receipt/{id}")]
    public async Task<IActionResult> DownloadReceipt(string id)
    {
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var invoice = await new InvoiceService().GetAsync(id);
        // Stripe.net 50.x: charge receipt URL is not directly accessible; redirect to hosted invoice
        if (string.IsNullOrEmpty(invoice?.HostedInvoiceUrl)) return NotFound();
        return Redirect(invoice.HostedInvoiceUrl);
    }

    // GET /admin/invoices — local myDATA-readiness invoice records (not a live myDATA submission)
    public async Task<IActionResult> Invoices()
    {
        var invoices = await _db.Invoices
            .Include(i => i.User)
            .OrderByDescending(i => i.IssuedAt)
            .Take(200)
            .ToListAsync();

        return View(invoices);
    }

    // GET /admin/tickets
    public async Task<IActionResult> Tickets(SupportTicketStatus? status)
    {
        var query = _db.SupportTickets
            .Include(t => t.Replies).ThenInclude(r => r.RepliedByUser)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var tickets = await query
            .OrderBy(t => t.Status == SupportTicketStatus.Open ? 0 : t.Status == SupportTicketStatus.Answered ? 1 : 2)
            .ThenByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.StatusFilter = status;
        return View(tickets);
    }

    // POST /admin/tickets/{id}/reply
    [HttpPost("/admin/tickets/{id}/reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToTicket(int id, string replyBody, [FromServices] IUserEmailService userEmail)
    {
        var ticket = await _db.SupportTickets.FindAsync(id);
        if (ticket is null) return NotFound();

        if (string.IsNullOrWhiteSpace(replyBody))
        {
            TempData["AdminMsg"] = "Η απάντηση δεν μπορεί να είναι κενή.";
            return RedirectToAction(nameof(Tickets));
        }

        var adminId = _userManager.GetUserId(User)!;
        _db.SupportTicketReplies.Add(new Models.Entities.SupportTicketReply
        {
            TicketId = id,
            Body = replyBody,
            RepliedByUserId = adminId,
        });
        ticket.Status = SupportTicketStatus.Answered;
        await _db.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            try { await userEmail.SendTicketReplyAsync(ticket, replyBody); }
            catch { /* best-effort */ }
        });

        TempData["AdminMsg"] = "Η απάντηση στάλθηκε.";
        return RedirectToAction(nameof(Tickets));
    }

    // POST /admin/tickets/{id}/close
    [HttpPost("/admin/tickets/{id}/close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseTicket(int id)
    {
        var ticket = await _db.SupportTickets.FindAsync(id);
        if (ticket is null) return NotFound();

        ticket.Status = SupportTicketStatus.Closed;
        await _db.SaveChangesAsync();

        TempData["AdminMsg"] = "Το αίτημα έκλεισε.";
        return RedirectToAction(nameof(Tickets));
    }
}
