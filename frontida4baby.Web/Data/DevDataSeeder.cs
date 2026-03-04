using Microsoft.AspNetCore.Identity;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Data;

/// <summary>
/// Seeds realistic demo data in Development only.
/// Safe to run repeatedly — checks for existing seed records first.
/// </summary>
public static class DevDataSeeder
{
    // ── Known credentials (all share the same password) ──────────────────────
    private static readonly SeedUser[] Users =
    [
        // Regular users (families seeking care)
        new("maria.papadopoulou@test.com", "Test1234!", "Maria",  "Papadopoulou", "Athens",       IsCaregiver: false),
        new("nikos.georgiou@test.com",     "Test1234!", "Nikos",  "Georgiou",     "Thessaloniki", IsCaregiver: false),
        new("sofia.alexiou@test.com",      "Test1234!", "Sofia",  "Alexiou",      "Patra",        IsCaregiver: false),

        // Caregivers
        new("elena.caregiver@test.com",    "Test1234!", "Elena",  "Nikolaou",     "Athens",       IsCaregiver: true),
        new("petros.caregiver@test.com",   "Test1234!", "Petros", "Andreou",      "Thessaloniki", IsCaregiver: true),
        new("anna.caregiver@test.com",     "Test1234!", "Anna",   "Kostopoulou",  "Athens",       IsCaregiver: true),
    ];

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db          = services.GetRequiredService<ApplicationDbContext>();

        // Skip if any seed user already exists
        if (await userManager.FindByEmailAsync(Users[0].Email) is not null)
            return;

        logger.LogInformation("DevDataSeeder: seeding demo users, posts and replies...");

        // ── 1. Create users ───────────────────────────────────────────────────
        var created = new Dictionary<string, ApplicationUser>();
        foreach (var s in Users)
        {
            var user = new ApplicationUser
            {
                UserName       = s.Email,
                Email          = s.Email,
                EmailConfirmed = true,
                FirstName      = s.FirstName,
                LastName       = s.LastName,
                City           = s.City,
                IsCaregiver    = s.IsCaregiver,
            };
            var result = await userManager.CreateAsync(user, s.Password);
            if (result.Succeeded)
                created[s.Email] = user;
            else
                logger.LogWarning("DevDataSeeder: could not create {Email}: {Errors}",
                    s.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        if (created.Count == 0) return;

        // Resolve all users upfront so they are available across every section
        created.TryGetValue("maria.papadopoulou@test.com", out var maria);
        created.TryGetValue("nikos.georgiou@test.com",     out var nikos);
        created.TryGetValue("sofia.alexiou@test.com",      out var sofia);
        created.TryGetValue("elena.caregiver@test.com",    out var elena);
        created.TryGetValue("petros.caregiver@test.com",   out var petros);
        created.TryGetValue("anna.caregiver@test.com",     out var anna);

        // ── 2. Create posts ───────────────────────────────────────────────────
        // posts[0] Maria childcare, [1] Maria tutoring,
        // posts[2] Nikos elderly,   [3] Nikos housekeeping,
        // posts[4] Sofia pet
        var posts = new List<Post>();

        if (maria is not null)
        {
            posts.Add(Approved(maria.Id,
                "Looking for a babysitter for two children in Athens",
                "I have two children aged 3 and 6. I need a reliable babysitter for afternoon hours (15:00-19:00), " +
                "Monday to Friday. Experience and references required. Compensation negotiable.",
                ServiceType.Childcare, "Athens"));

            posts.Add(Approved(maria.Id,
                "Looking for an English tutor for a primary school child",
                "I am looking for an English teacher for my child who is in 3rd grade of primary school. " +
                "Twice a week, preferably afternoon hours. Area: Kifisia.",
                ServiceType.Tutoring, "Athens"));
        }

        if (nikos is not null)
        {
            posts.Add(Approved(nikos.Id,
                "Help needed for elderly parent in Thessaloniki",
                "I am looking for someone for companionship and assistance with the daily needs of my father (78 years old). " +
                "Every day 10:00-14:00. Patience and experience with the elderly is essential.",
                ServiceType.ElderlyCare, "Thessaloniki"));

            posts.Add(Approved(nikos.Id,
                "Looking for a housekeeping assistant in Thessaloniki",
                "I need a housekeeper for weekly cleaning of a three-room apartment. " +
                "Every Saturday morning, approximately 4 hours. Own cleaning supplies preferred.",
                ServiceType.Housekeeping, "Thessaloniki"));
        }

        if (sofia is not null)
        {
            posts.Add(Approved(sofia.Id,
                "Pet sitter needed for dog in Patra",
                "I am looking for a pet sitter for my dog (Golden Retriever, 3 years old) during a 10-day vacation in August. " +
                "I prefer the dog to stay at the pet sitter's home. Vaccinated and friendly.",
                ServiceType.PetCare, "Patra"));
        }

        db.Posts.AddRange(posts);
        await db.SaveChangesAsync();

        // ── 3. Create replies ─────────────────────────────────────────────────
        var replies = new List<Reply>();

        if (posts.Count > 0 && elena is not null)
            replies.Add(ApprovedReply(posts[0].Id, elena.Id,
                "Hello! I have 5 years of experience with children aged 2-8 and can provide references. " +
                "Can we arrange a meeting to get to know each other?"));

        if (posts.Count > 0 && anna is not null)
            replies.Add(ApprovedReply(posts[0].Id, anna.Id,
                "I am also interested! I have experience with infants and toddlers and am available during the hours you mention."));

        if (posts.Count > 0 && maria is not null)
            replies.Add(ApprovedReply(posts[0].Id, maria.Id,
                "Thank you for your interest! I will contact you soon."));

        if (posts.Count > 1 && petros is not null)
            replies.Add(ApprovedReply(posts[1].Id, petros.Id,
                "I am an English teacher with a degree and 3 years of private tutoring. Please contact me if you are interested."));

        if (posts.Count > 2 && anna is not null)
            replies.Add(ApprovedReply(posts[2].Id, anna.Id,
                "I have been caring for the elderly for 6 years. I have first aid training. " +
                "I would be happy to help your father."));

        if (posts.Count > 3 && elena is not null)
            replies.Add(ApprovedReply(posts[3].Id, elena.Id,
                "I am available on Saturdays and have experience with housekeeping. I bring my own supplies. Let me know!"));

        if (posts.Count > 4 && elena is not null)
            replies.Add(ApprovedReply(posts[4].Id, elena.Id,
                "I love animals and have cared for dogs in the past. I have a large house with a garden. " +
                "Contact me for details!"));

        db.Replies.AddRange(replies);
        await db.SaveChangesAsync();

        // ── 4. Reactions & saves ──────────────────────────────────────────────
        var reactions = new List<PostReaction>();
        var saves     = new List<SavedPost>();

        if (posts.Count >= 3 && elena is not null)
        {
            reactions.Add(new PostReaction { PostId = posts[0].Id, UserId = elena.Id });
            reactions.Add(new PostReaction { PostId = posts[2].Id, UserId = elena.Id });
            saves.Add(new SavedPost { PostId = posts[0].Id, UserId = elena.Id });
        }

        if (posts.Count >= 2 && petros is not null)
        {
            reactions.Add(new PostReaction { PostId = posts[1].Id, UserId = petros.Id });
            saves.Add(new SavedPost { PostId = posts[1].Id, UserId = petros.Id });
        }

        if (posts.Count >= 4 && anna is not null)
        {
            reactions.Add(new PostReaction { PostId = posts[0].Id, UserId = anna.Id });
            reactions.Add(new PostReaction { PostId = posts[3].Id, UserId = anna.Id });
            saves.Add(new SavedPost { PostId = posts[2].Id, UserId = anna.Id });
        }

        if (posts.Count >= 1 && nikos is not null)
            saves.Add(new SavedPost { PostId = posts[0].Id, UserId = nikos.Id });

        db.PostReactions.AddRange(reactions);
        db.SavedPosts.AddRange(saves);
        await db.SaveChangesAsync();

        // ── 5. Print credentials to console ──────────────────────────────────
        logger.LogInformation("==========================================================");
        logger.LogInformation("  DEV SEED ACCOUNTS  (all passwords: Test1234!)");
        logger.LogInformation("  FAMILIES");
        logger.LogInformation("    maria.papadopoulou@test.com  (Athens,       2 posts)");
        logger.LogInformation("    nikos.georgiou@test.com      (Thessaloniki, 2 posts)");
        logger.LogInformation("    sofia.alexiou@test.com       (Patra,        1 post)");
        logger.LogInformation("  CAREGIVERS");
        logger.LogInformation("    elena.caregiver@test.com     (Athens)");
        logger.LogInformation("    petros.caregiver@test.com    (Thessaloniki)");
        logger.LogInformation("    anna.caregiver@test.com      (Athens)");
        logger.LogInformation("==========================================================");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Post Approved(string authorId, string title, string body,
        ServiceType? serviceType = null, string? city = null) => new()
    {
        AuthorUserId     = authorId,
        Title            = title,
        Body             = body,
        ServiceType      = serviceType,
        City             = city,
        Status           = PostStatus.Active,
        ModerationStatus = ModerationStatus.Approved,
    };

    private static Reply ApprovedReply(int postId, string authorId, string body) => new()
    {
        PostId           = postId,
        AuthorUserId     = authorId,
        Body             = body,
        ModerationStatus = ModerationStatus.Approved,
    };

    private record SeedUser(
        string Email, string Password,
        string FirstName, string LastName, string City,
        bool IsCaregiver);
}
