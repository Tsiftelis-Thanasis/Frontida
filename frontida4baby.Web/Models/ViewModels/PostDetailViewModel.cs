using System.ComponentModel.DataAnnotations;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class PostDetailViewModel
{
    public int          PostId            { get; set; }
    public string       Title             { get; set; } = string.Empty;
    public string       Body              { get; set; } = string.Empty;
    public string       AuthorName        { get; set; } = string.Empty;
    public string       AuthorId          { get; set; } = string.Empty;
    public ServiceType? ServiceType       { get; set; }
    public string?      City              { get; set; }
    public PostStatus        Status            { get; set; }
    public ModerationStatus  ModerationStatus  { get; set; }
    public DateTime          CreatedAt         { get; set; }
    public DateTime?    EditedAt          { get; set; }
    public int          ReactionCount     { get; set; }
    public bool         CurrentUserLiked  { get; set; }
    public bool         CurrentUserSaved  { get; set; }
    public bool         CanEdit              { get; set; }
    public bool         CanCloseAsAdmin      { get; set; }
    public string?      ClosedReason         { get; set; }
    public bool         CanViewAuthorProfile { get; set; }
    public bool         ViewerIsCaregiver    { get; set; }
    public bool         AuthorIsCaregiver    { get; set; }
    public double       AuthorAverageRating  { get; set; }
    public int          AuthorReviewCount    { get; set; }
    public bool         CanSeeRatingDetails  { get; set; }
    public List<ReviewDetailItem> AuthorReviews { get; set; } = new();

    public List<ReplyItemViewModel> Replies { get; set; } = new();

    // Reaction approval
    public List<ReactingUserViewModel> ReactingUsers        { get; set; } = new();
    public bool                        IsViewerApprovedReactor { get; set; }
    public bool                        ShowOPContactInfo    { get; set; }

    // OP contact info (only shown when approved)
    public string? AuthorPhone   { get; set; }
    public string? AuthorEmail   { get; set; }
    public string? AuthorAddress { get; set; }

    // Inline reply form
    [Required(ErrorMessage = "Reply text is required.")]
    [StringLength(2000, MinimumLength = 5,
        ErrorMessage = "Reply must be between 5 and 2000 characters.")]
    [Display(Name = "Your reply")]
    public string ReplyBody { get; set; } = string.Empty;
}

public class ReplyItemViewModel
{
    public int       Id                  { get; set; }
    public string    AuthorName          { get; set; } = string.Empty;
    public string    AuthorId            { get; set; } = string.Empty;
    public bool      AuthorIsCaregiver   { get; set; }
    public double    AuthorAverageRating { get; set; }
    public int       AuthorReviewCount   { get; set; }
    public string    Body                { get; set; } = string.Empty;
    public DateTime  CreatedAt           { get; set; }
    public DateTime? EditedAt            { get; set; }
    public bool      CanEdit             { get; set; }
}

public class ReactingUserViewModel
{
    public int      ReactionId      { get; set; }
    public string   UserId          { get; set; } = string.Empty;
    public string   UserName        { get; set; } = string.Empty;
    public bool     IsCaregiver     { get; set; }
    public double   AverageRating   { get; set; }
    public int      ReviewCount     { get; set; }
    public bool     IsApproved      { get; set; }
    public DateTime ReactedAt       { get; set; }
}

public class ReviewDetailItem
{
    public string    ReviewerName { get; set; } = string.Empty;
    public int       Rating       { get; set; }
    public string?   Comment      { get; set; }
    public DateTime  CreatedAt    { get; set; }
}
