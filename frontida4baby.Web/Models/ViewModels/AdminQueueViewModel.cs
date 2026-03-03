using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Models.ViewModels;

public class AdminQueueViewModel
{
    public List<PendingPostViewModel>  PendingPosts   { get; set; } = new();
    public List<PendingReplyViewModel> PendingReplies { get; set; } = new();
}

public class PendingPostViewModel
{
    public int      Id         { get; set; }
    public string   Title      { get; set; } = string.Empty;
    public string   Body       { get; set; } = string.Empty;
    public string   AuthorName { get; set; } = string.Empty;
    public string?  City       { get; set; }
    public ServiceType? ServiceType { get; set; }
    public DateTime CreatedAt  { get; set; }
}

public class PendingReplyViewModel
{
    public int      Id        { get; set; }
    public int      PostId    { get; set; }
    public string   PostTitle { get; set; } = string.Empty;
    public string   Body      { get; set; } = string.Empty;
    public string   AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
