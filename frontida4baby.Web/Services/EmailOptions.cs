namespace frontida4baby.Web.Services;

public class EmailOptions
{
    public string SmtpHost    { get; set; } = "smtp.gmail.com";
    public int    SmtpPort    { get; set; } = 587;
    public string SmtpUser    { get; set; } = "";
    public string SmtpPass    { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName    { get; set; } = "frontida4all";
    public string AdminEmail  { get; set; } = "";
}
