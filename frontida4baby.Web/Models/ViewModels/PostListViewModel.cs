using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class PostListViewModel
{
    public ServiceType? ServiceType { get; set; }
    public string?      City        { get; set; }
    public List<PostListItemViewModel> Posts { get; set; } = new();
}

public class PostListItemViewModel
{
    public int          Id                  { get; set; }
    public string       Title               { get; set; } = string.Empty;
    public string       AuthorName          { get; set; } = string.Empty;
    public bool         AuthorIsCaregiver   { get; set; }
    public double       AuthorAverageRating { get; set; }
    public int          AuthorReviewCount   { get; set; }
    public ServiceType? ServiceType         { get; set; }
    public string?      City                { get; set; }
    public int          ReplyCount          { get; set; }
    public DateTime     CreatedAt           { get; set; }
}
