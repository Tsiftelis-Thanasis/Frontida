namespace frontida4baby.Web.Models.ViewModels;

public class PublicProfileViewModel
{
    public string  UserId             { get; set; } = "";
    public string  FullName           { get; set; } = "";
    public string? City               { get; set; }
    public bool    IsCaregiver        { get; set; }
    public string? Bio                { get; set; }
    public string? Phone              { get; set; }
    public bool    PhoneVisible       { get; set; }
    public double  AverageRating      { get; set; }
    public int     ReviewCount        { get; set; }
    public bool    CanSeeRatingDetails { get; set; }
    public List<ReviewDetailItem>      Reviews     { get; set; } = new();
    public List<PostListItemViewModel> RecentPosts { get; set; } = new();
}
