using frontida4baby.Web.Data;
using frontida4baby.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace frontida4baby.Web.Controllers;

[Authorize]
public class SavedPostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SavedPostsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpPost("/saved/toggle/{postId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int postId)
    {
        var userId = _userManager.GetUserId(User)!;

        // Validate post exists and is active + approved
        var post = await _db.Posts.FindAsync(postId);
        if (post is null || post.Status != PostStatus.Active
            || post.ModerationStatus != ModerationStatus.Approved)
            return Json(new { error = "Cannot save this post." });

        var existing = await _db.SavedPosts
            .FirstOrDefaultAsync(s => s.PostId == postId && s.UserId == userId);

        bool saved;
        if (existing != null)
        {
            _db.SavedPosts.Remove(existing);
            saved = false;
        }
        else
        {
            _db.SavedPosts.Add(new SavedPost
            {
                PostId = postId,
                UserId = userId,
            });
            saved = true;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent double-submit — check current state
            saved = await _db.SavedPosts.AnyAsync(s => s.PostId == postId && s.UserId == userId);
        }

        return Json(new { saved });
    }
}
