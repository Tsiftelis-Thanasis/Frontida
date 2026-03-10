using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class DashboardViewModel
{
    public List<PostListItemViewModel> MyPosts      { get; set; } = new();
    public List<ReplyDashboardItem>    MyReplies    { get; set; } = new();
    public List<PostListItemViewModel> ReactedPosts { get; set; } = new();
    public List<PostListItemViewModel> SavedPosts   { get; set; } = new();
    public SubscriptionPlan            CurrentPlan      { get; set; }
    public string                      ActiveTab        { get; set; } = "posts";
    public bool                        IsEmailVerified  { get; set; }
}

public class ReplyDashboardItem
{
    public int      ReplyId   { get; set; }
    public int      PostId    { get; set; }
    public string   PostTitle { get; set; } = "";
    public string   Body      { get; set; } = "";
    public DateTime  CreatedAt { get; set; }
    public DateTime? EditedAt  { get; set; }
}
