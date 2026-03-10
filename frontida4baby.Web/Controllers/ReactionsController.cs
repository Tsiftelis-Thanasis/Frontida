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

    public ReactionsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscription)
    {
        _db = db;
        _userManager = userManager;
        _subscription = subscription;
    }

    [HttpPost("/reactions/toggle/{postId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int postId)
    {
        var userId = _userManager.GetUserId(User)!;

        var reactingUser = await _userManager.FindByIdAsync(userId);
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

        await _db.SaveChangesAsync();

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

        TempData["ReactionApproved"] = true;
        return RedirectToAction("Details", "Posts", new { id = reaction.PostId });
    }
}
