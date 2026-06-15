using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Controllers;

[Authorize]
public class ReactionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscription;
    private readonly IUserEmailService _userEmail;

    public ReactionsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscription,
        IUserEmailService userEmail)
    {
        _db = db;
        _userManager = userManager;
        _subscription = subscription;
        _userEmail = userEmail;
    }

    [HttpPost("/reactions/toggle/{postId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int postId)
    {
        var userId = _userManager.GetUserId(User)!;

        var post = await _db.Posts.FindAsync(postId);
        if (post is null || post.ModerationStatus != ModerationStatus.Approved
            || post.Status != PostStatus.Active)
            return Json(new { error = "Cannot react to this post." });

        var reactingUser = await _userManager.FindByIdAsync(userId);
        if (reactingUser is not null && !reactingUser.EmailConfirmed)
            return Json(new { error = "Please confirm your email address first." });
        if (reactingUser?.IsBlacklisted == true)
            return Json(new { error = "Ο λογαριασμός σας έχει αποκλειστεί. Δεν μπορείτε να αντιδράτε σε αγγελίες." });

        var existing = await _db.PostReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

        bool liked;
        if (existing != null)
        {
            _db.PostReactions.Remove(existing);
            liked = false;
        }
        else
        {
            if (!await _subscription.CanReactAsync(userId))
                return Json(new { error = "Monthly reaction limit reached. Upgrade to Premium for unlimited reactions." });

            _db.PostReactions.Add(new PostReaction
            {
                PostId = postId,
                UserId = userId,
            });
            liked = true;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent double-submit — treat as already-applied
            var currentLiked = await _db.PostReactions.AnyAsync(r => r.PostId == postId && r.UserId == userId);
            var currentCount = await _db.PostReactions.CountAsync(r => r.PostId == postId);
            return Json(new { liked = currentLiked, count = currentCount });
        }

        var count = await _db.PostReactions.CountAsync(r => r.PostId == postId);
        return Json(new { liked, count });
    }

    [HttpPost("/reactions/approve/{reactionId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveReaction(int reactionId)
    {
        var userId   = _userManager.GetUserId(User)!;
        var reaction = await _db.PostReactions
            .Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == reactionId);

        if (reaction is null) return NotFound();

        // Only the OP of the post can approve reactions
        if (reaction.Post.AuthorUserId != userId) return Forbid();

        reaction.IsApprovedByOP = true;
        reaction.ApprovedAt     = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var caregiver = await _userManager.FindByIdAsync(reaction.UserId);
        var op        = await _userManager.FindByIdAsync(reaction.Post.AuthorUserId);
        if (caregiver is not null && op is not null)
            _ = Task.Run(async () =>
            {
                try { await _userEmail.SendReactionApprovedAsync(caregiver, reaction.Post.Title, op); }
                catch { /* best-effort */ }
            });

        TempData["ReactionApproved"] = true;
        return RedirectToAction("Details", "Posts", new { id = reaction.PostId });
    }
}
