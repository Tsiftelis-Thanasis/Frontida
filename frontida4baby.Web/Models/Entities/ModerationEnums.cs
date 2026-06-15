namespace frontida4baby.Web.Models.Entities;

public enum ModerationStatus  { Approved, Rejected, PendingReview }
public enum PostStatus        { Active, Closed, Deleted }
public enum ContentType       { Post, Reply }
public enum ModerationStage   { Wordlist, Claude, Manual }
public enum SubscriptionPlan  { Free, Paid }
public enum SubscriptionStatus { Active, Cancelled, Expired }
public enum AppLogLevel       { Info, Warning, Error }
