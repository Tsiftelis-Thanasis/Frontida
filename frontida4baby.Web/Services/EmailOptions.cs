namespace frontida4baby.Web.Services;

public class EmailOptions
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPass { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@frontida4baby.gr";
    public string FromName { get; set; } = "frontida4baby";
}
