using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;

namespace frontida4baby.Web.Controllers;

public class PostsController : Controller
{
    private readonly ApplicationDbContext      _db;
    private readonly IContentModerationService _moderation;
    private readonly ContentModerationService  _moderationLog;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscription;
    private readonly IAppLogService       _log;
    private readonly INotificationService _notifications;

    public PostsController(
        ApplicationDbContext      db,
        IContentModerationService moderation,
        ContentModerationService  moderationLog,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscription,
        IAppLogService       log,
        INotificationService notifications)
    {
        _db            = db;
        _moderation    = moderation;
        _moderationLog = moderationLog;
        _userManager   = userManager;
        _subscription  = subscription;
        _log           = log;
        _notifications = notifications;
    }

    // ── GET /posts ────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(PostListViewModel filter)
    {
        var currentUserId = _userManager.GetUserId(User);

        var query = _db.Posts
            .AsNoTracking()
            .Include(p => p.AuthorUser).ThenInclude(u => u.ReceivedReviews)
            .Include(p => p.Replies)
            .Where(p => (p.Status == PostStatus.Active || p.AuthorUserId == currentUserId)
                     && p.Status != PostStatus.Deleted)
            .Where(p => p.ModerationStatus == ModerationStatus.Approved
                     || p.AuthorUserId == currentUserId);

        if (filter.ServiceType.HasValue)
            query = query.Where(p => p.ServiceType == filter.ServiceType.Value);

        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(p => p.City != null &&
                p.City.Contains(filter.City));

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PostListItemViewModel
            {
                Id                  = p.Id,
                Title               = p.Title,
                AuthorName          = $"{p.AuthorUser.FirstName} {p.AuthorUser.LastName}".Trim(),
                AuthorIsCaregiver   = p.AuthorUser.IsCaregiver,
                AuthorAverageRating = p.AuthorUser.ReceivedReviews.Any() ? p.AuthorUser.ReceivedReviews.Average(r => (double)r.Rating) : 0,
                AuthorReviewCount   = p.AuthorUser.ReceivedReviews.Count(),
                ServiceType         = p.ServiceType,
                City                = p.City,
                ReplyCount          = p.Replies.Count(r => r.ModerationStatus == ModerationStatus.Approved),
                CreatedAt           = p.CreatedAt,
                Status              = p.Status,
                ModerationStatus    = p.ModerationStatus,
                IsOwner             = p.AuthorUserId == currentUserId,
            })
            .ToListAsync();

        filter.Posts = posts;
        return View(filter);
    }

    // ── GET /posts/details/5 ─────────────────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var post = await _db.Posts
            .Include(p => p.AuthorUser)
                .ThenInclude(u => u.ReceivedReviews)
                    .ThenInclude(rv => rv.ReviewerUser)
            .Include(p => p.Replies)
                .ThenInclude(r => r.AuthorUser)
                    .ThenInclude(u => u.ReceivedReviews)
            .Include(p => p.Reactions)
                .ThenInclude(r => r.User)
                    .ThenInclude(u => u.ReceivedReviews)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id
                && (p.Status == PostStatus.Active || p.Status == PostStatus.Closed));

        if (post is null) return NotFound();

        var currentUserId = _userManager.GetUserId(User);

        int reactionCount = await _db.PostReactions.CountAsync(r => r.PostId == id);
        bool liked = currentUserId != null &&
            await _db.PostReactions.AnyAsync(r => r.PostId == id && r.UserId == currentUserId);

        // Reaction approval: check if the current viewer has an approved reaction
        bool isViewerApprovedReactor = currentUserId != null &&
            post.Reactions.Any(r => r.UserId == currentUserId && r.IsApprovedByOP);

        // OP can always see their own contact; approved reactors also see it
        bool isOP = currentUserId == post.AuthorUserId;
        bool showOPContact = isOP || isViewerApprovedReactor;
        bool saved = currentUserId != null &&
            await _db.SavedPosts.AnyAsync(s => s.PostId == id && s.UserId == currentUserId);

        var viewerUser = currentUserId != null
            ? await _userManager.FindByIdAsync(currentUserId)
            : null;
        bool viewerIsCaregiver = viewerUser?.IsCaregiver ?? false;
        bool canViewProfile    = false;
        if (viewerIsCaregiver && currentUserId != null && currentUserId != post.AuthorUserId)
            canViewProfile = await _subscription.CanViewProfileAsync(currentUserId, post.AuthorUserId);

        bool isAdmin = User.IsInRole("Admin");
        bool isPaid  = !isAdmin && currentUserId != null &&
            await _db.Subscriptions.AnyAsync(s =>
                s.UserId == currentUserId &&
                s.Plan   == SubscriptionPlan.Paid &&
                s.Status == SubscriptionStatus.Active);
        bool canSeeRatingDetails = isAdmin || isPaid;

        var authorReviews = canSeeRatingDetails
            ? post.AuthorUser.ReceivedReviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDetailItem
                {
                    ReviewerName = $"{r.ReviewerUser.FirstName} {r.ReviewerUser.LastName}".Trim(),
                    Rating       = r.Rating,
                    Comment      = r.Comment,
                    CreatedAt    = r.CreatedAt,
                }).ToList()
            : new List<ReviewDetailItem>();

        var vm = new PostDetailViewModel
        {
            PostId               = post.Id,
            Title                = post.Title,
            Body                 = post.Body,
            AuthorName           = $"{post.AuthorUser.FirstName} {post.AuthorUser.LastName}".Trim(),
            AuthorId             = post.AuthorUserId,
            AuthorIsCaregiver    = post.AuthorUser.IsCaregiver,
            AuthorAverageRating  = post.AuthorUser.ReceivedReviews.Any() ? post.AuthorUser.ReceivedReviews.Average(r => (double)r.Rating) : 0,
            AuthorReviewCount    = post.AuthorUser.ReceivedReviews.Count,
            CanSeeRatingDetails  = canSeeRatingDetails,
            AuthorReviews        = authorReviews,
            ServiceType          = post.ServiceType,
            City                 = post.City,
            Status               = post.Status,
            ModerationStatus     = post.ModerationStatus,
            CreatedAt            = post.CreatedAt,
            EditedAt             = post.EditedAt,
            ReactionCount        = reactionCount,
            CurrentUserLiked     = liked,
            CurrentUserSaved     = saved,
            CanEdit              = currentUserId == post.AuthorUserId,
            CanCloseAsAdmin      = isAdmin && currentUserId != post.AuthorUserId && post.Status == PostStatus.Active,
            ClosedReason         = post.ClosedReason,
            ViewerIsCaregiver    = viewerIsCaregiver,
            CanViewAuthorProfile = canViewProfile,
            IsViewerApprovedReactor = isViewerApprovedReactor,
            ShowOPContactInfo    = showOPContact,
            AuthorPhone          = showOPContact ? post.AuthorUser.PhoneNumber : null,
            AuthorEmail          = showOPContact ? post.AuthorUser.Email : null,
            AuthorAddress        = showOPContact ? $"{post.AuthorUser.Address}, {post.AuthorUser.City}".Trim(',', ' ') : null,
            ReactingUsers        = isOP ? post.Reactions
                .OrderBy(r => r.CreatedAt)
                .Select(r => new ReactingUserViewModel
                {
                    ReactionId    = r.Id,
                    UserId        = r.UserId,
                    UserName      = $"{r.User.FirstName} {r.User.LastName}".Trim(),
                    IsCaregiver   = r.User.IsCaregiver,
                    AverageRating = r.User.ReceivedReviews.Any() ? r.User.ReceivedReviews.Average(rv => (double)rv.Rating) : 0,
                    ReviewCount   = r.User.ReceivedReviews.Count,
                    IsApproved    = r.IsApprovedByOP,
                    ReactedAt     = r.CreatedAt,
                }).ToList() : new(),
            Replies          = post.Replies
                .Where(r => r.ModerationStatus == ModerationStatus.Approved)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new ReplyItemViewModel
                {
                    Id                  = r.Id,
                    AuthorName          = $"{r.AuthorUser.FirstName} {r.AuthorUser.LastName}".Trim(),
                    AuthorId            = r.AuthorUserId,
                    AuthorIsCaregiver   = r.AuthorUser.IsCaregiver,
                    AuthorAverageRating = r.AuthorUser.ReceivedReviews.Any() ? r.AuthorUser.ReceivedReviews.Average(rv => (double)rv.Rating) : 0,
                    AuthorReviewCount   = r.AuthorUser.ReceivedReviews.Count,
                    Body                = r.Body,
                    CreatedAt           = r.CreatedAt,
                    EditedAt            = r.EditedAt,
                    CanEdit             = r.AuthorUserId == currentUserId,
                })
                .ToList(),
        };

        return View(vm);
    }

    // ── GET /posts/create ─────────────────────────────────────────────────────
    [Authorize]
    public IActionResult Create() => View(new CreatePostViewModel());

    // ── POST /posts/create ────────────────────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("content-create")]
    public async Task<IActionResult> Create(CreatePostViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userId = _userManager.GetUserId(User)!;

        var currentUser = await _userManager.FindByIdAsync(userId);
        if (currentUser is not null && !currentUser.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty,
                "Please confirm your email address before posting. Check your inbox or resend from your dashboard.");
            return View(model);
        }
        if (currentUser?.IsBlacklisted == true)
        {
            ModelState.AddModelError(string.Empty,
                "Ο λογαριασμός σας έχει αποκλειστεί από τη δημοσίευση περιεχομένου λόγω επαναλαμβανόμενων παραβιάσεων. Επικοινωνήστε με την υποστήριξη για βοήθεια.");
            return View(model);
        }

        if (!await _subscription.CanPostAsync(userId))
        {
            ModelState.AddModelError(string.Empty,
                "Έχετε φτάσει το μηνιαίο όριο 5 αγγελιών. Αναβαθμίστε σε Premium για απεριόριστες αγγελίες.");
            return View(model);
        }

        var result = await _moderation.ModerateAsync(model.Title, model.Body);

        if (result.Status == ModerationStatus.Rejected)
        {
            // Log the rejection so it counts toward auto-blacklist threshold
            await _moderationLog.LogAsync(ContentType.Post, 0, userId,
                model.Title, model.Body, result);

            ModelState.AddModelError(string.Empty,
                $"Your post could not be published: {result.Reason}");
            return View(model);
        }

        var post = new Post
        {
            AuthorUserId      = userId,
            Title             = model.Title,
            Body              = model.Body,
            ServiceType       = model.ServiceType,
            City              = model.City,
            ModerationStatus  = result.Status,
            ModerationReason  = result.Reason,
            Status            = PostStatus.Active,
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        await _moderationLog.LogAsync(ContentType.Post, post.Id, userId,
            model.Title, model.Body, result);
        await _log.LogAsync(AppLogLevel.Info, "Post",
            $"Created post #{post.Id}: \"{model.Title}\"", userId: userId);

        if (result.Status == ModerationStatus.PendingReview)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notifications.NotifyPendingReviewAsync(
                        "Post", currentUser?.Email ?? userId, $"{model.Title}\n{model.Body}");
                }
                catch { /* best-effort */ }
            });

            TempData["PostStatus"] = "Pending";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    // ── POST /posts/reply ─────────────────────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("content-create")]
    public async Task<IActionResult> Reply(int id, PostDetailViewModel model)
    {
        var post = await _db.Posts
            .Include(p => p.AuthorUser)
            .Include(p => p.Replies).ThenInclude(r => r.AuthorUser)
            .FirstOrDefaultAsync(p => p.Id == id
                && p.ModerationStatus == ModerationStatus.Approved
                && p.Status == PostStatus.Active);

        if (post is null) return NotFound();

        // Re-populate non-form fields so View() can render correctly on error
        var currentUserId = _userManager.GetUserId(User)!;
        model.PostId      = post.Id;
        model.Title       = post.Title;
        model.Body        = post.Body;
        model.AuthorName  = $"{post.AuthorUser.FirstName} {post.AuthorUser.LastName}".Trim();
        model.AuthorId    = post.AuthorUserId;
        model.ServiceType = post.ServiceType;
        model.City        = post.City;
        model.Status      = post.Status;
        model.CreatedAt   = post.CreatedAt;
        model.Replies     = post.Replies
            .Where(r => r.ModerationStatus == ModerationStatus.Approved)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReplyItemViewModel
            {
                Id         = r.Id,
                AuthorName = $"{r.AuthorUser.FirstName} {r.AuthorUser.LastName}".Trim(),
                AuthorId   = r.AuthorUserId,
                Body       = r.Body,
                CreatedAt  = r.CreatedAt,
                EditedAt   = r.EditedAt,
                CanEdit    = r.AuthorUserId == currentUserId,
            }).ToList();

        var replyUser = await _userManager.FindByIdAsync(currentUserId);
        if (replyUser is not null && !replyUser.EmailConfirmed)
        {
            ModelState.AddModelError(nameof(model.ReplyBody),
                "Please confirm your email address before replying.");
            return View(nameof(Details), model);
        }

        if (!await _subscription.CanReplyAsync(currentUserId))
        {
            ModelState.AddModelError(string.Empty,
                "Έχετε φτάσει το μηνιαίο όριο 20 απαντήσεων. Αναβαθμίστε σε Premium για απεριόριστες απαντήσεις.");
            return View(nameof(Details), model);
        }

        // Validate only the reply body
        if (string.IsNullOrWhiteSpace(model.ReplyBody) ||
            model.ReplyBody.Length < 5 || model.ReplyBody.Length > 2000)
        {
            ModelState.AddModelError(nameof(model.ReplyBody),
                "Reply must be between 5 and 2000 characters.");
            return View(nameof(Details), model);
        }

        var result = await _moderation.ModerateAsync(null, model.ReplyBody);

        if (result.Status == ModerationStatus.Rejected)
        {
            // Log the rejection so it counts toward auto-blacklist threshold
            await _moderationLog.LogAsync(ContentType.Reply, 0, currentUserId,
                null, model.ReplyBody, result);

            ModelState.AddModelError(nameof(model.ReplyBody),
                $"Your reply could not be posted: {result.Reason}");
            return View(nameof(Details), model);
        }

        var reply = new Reply
        {
            PostId           = post.Id,
            AuthorUserId     = currentUserId,
            Body             = model.ReplyBody,
            ModerationStatus = result.Status,
            ModerationReason = result.Reason,
        };

        _db.Replies.Add(reply);
        await _db.SaveChangesAsync();

        await _moderationLog.LogAsync(ContentType.Reply, reply.Id, currentUserId,
            null, model.ReplyBody, result);
        await _log.LogAsync(AppLogLevel.Info, "Post",
            $"Reply #{reply.Id} on post #{post.Id}", userId: currentUserId);

        if (result.Status == ModerationStatus.PendingReview)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notifications.NotifyPendingReviewAsync(
                        "Reply", replyUser?.Email ?? currentUserId, model.ReplyBody);
                }
                catch { /* best-effort */ }
            });
        }

        TempData["ReplyStatus"] = result.Status == ModerationStatus.Approved
            ? "Approved" : "Pending";

        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    // ── POST /posts/close ─────────────────────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id, string? reason = null, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User);
        var post   = await _db.Posts.FindAsync(id);

        bool isAdmin = User.IsInRole("Admin");
        if (post is null || (post.AuthorUserId != userId && !isAdmin)) return Forbid();

        post.Status = PostStatus.Closed;

        // Only record an admin closure comment when an admin closes someone else's post
        if (isAdmin && post.AuthorUserId != userId && !string.IsNullOrWhiteSpace(reason))
        {
            post.ClosedReason   = reason;
            post.ClosedByUserId = userId;
            post.ClosedAt       = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // ── GET /posts/edit/5 ─────────────────────────────────────────────────────
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _userManager.GetUserId(User);
        var post   = await _db.Posts.FindAsync(id);

        if (post is null) return NotFound();
        if (post.AuthorUserId != userId) return Forbid();

        var vm = new CreatePostViewModel
        {
            Title       = post.Title,
            Body        = post.Body,
            ServiceType = post.ServiceType,
            City        = post.City,
        };
        ViewBag.PostId   = id;
        ViewBag.EditedAt = post.EditedAt;
        return View(vm);
    }

    // ── POST /posts/edit/5 ────────────────────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CreatePostViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PostId = id;
            return View(model);
        }

        var userId = _userManager.GetUserId(User);
        var post   = await _db.Posts.FindAsync(id);

        if (post is null) return NotFound();
        if (post.AuthorUserId != userId) return Forbid();

        var result = await _moderation.ModerateAsync(model.Title, model.Body);

        if (result.Status == ModerationStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty,
                $"Your post could not be updated: {result.Reason}");
            ViewBag.PostId = id;
            return View(model);
        }

        post.Title            = model.Title;
        post.Body             = model.Body;
        post.ServiceType      = model.ServiceType;
        post.City             = model.City;
        post.EditedAt         = DateTime.UtcNow;
        post.ModerationStatus = result.Status;
        post.ModerationReason = result.Reason;

        await _db.SaveChangesAsync();
        await _moderationLog.LogAsync(ContentType.Post, post.Id, userId!,
            model.Title, model.Body, result);

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── POST /posts/reply-edit/5 ──────────────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyEdit(int replyId, string body)
    {
        var userId = _userManager.GetUserId(User);
        var reply  = await _db.Replies.FindAsync(replyId);

        if (reply is null) return NotFound();
        if (reply.AuthorUserId != userId) return Forbid();

        if (string.IsNullOrWhiteSpace(body) || body.Length < 5 || body.Length > 2000)
        {
            TempData["ReplyEditError"] = "Reply must be between 5 and 2000 characters.";
            return RedirectToAction(nameof(Details), new { id = reply.PostId });
        }

        var result = await _moderation.ModerateAsync(null, body);

        if (result.Status == ModerationStatus.Rejected)
        {
            TempData["ReplyEditError"] = $"Your reply could not be updated: {result.Reason}";
            return RedirectToAction(nameof(Details), new { id = reply.PostId });
        }

        reply.Body             = body;
        reply.EditedAt         = DateTime.UtcNow;
        reply.ModerationStatus = result.Status;
        reply.ModerationReason = result.Reason;

        await _db.SaveChangesAsync();
        await _moderationLog.LogAsync(ContentType.Reply, reply.Id, userId!,
            null, body, result);

        return RedirectToAction(nameof(Details), new { id = reply.PostId });
    }
}
