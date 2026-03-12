namespace frontida4baby.Web.Services;

public class UserEmailOptions
{
    public bool Welcome               { get; set; } = true;
    public bool ContentRejected       { get; set; } = true;
    public bool ReactionApproved      { get; set; } = true;
    public bool PaymentSucceeded      { get; set; } = true;
    public bool PaymentFailed         { get; set; } = true;
    public bool SubscriptionCancelled { get; set; } = true;
    public bool SupportConfirmation   { get; set; } = true;
}
