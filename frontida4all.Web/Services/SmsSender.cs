using System.Net.Http.Headers;
using System.Text;

namespace frontida4all.Web.Services;

/// <summary>
/// Sends SMS through Twilio's REST API (Twilio:AccountSid, Twilio:AuthToken and either Twilio:From
/// or Twilio:MessagingServiceSid). In Development without configuration the message is only logged.
/// </summary>
public class SmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;
    private readonly ILogger<SmsSender> _logger;

    public SmsSender(HttpClient http, IConfiguration cfg, IHostEnvironment env, ILogger<SmsSender> logger)
    {
        _http = http; _cfg = cfg; _env = env; _logger = logger;
    }

    private bool Configured =>
        !string.IsNullOrWhiteSpace(_cfg["Twilio:AccountSid"])
        && !string.IsNullOrWhiteSpace(_cfg["Twilio:AuthToken"])
        && (!string.IsNullOrWhiteSpace(_cfg["Twilio:From"]) || !string.IsNullOrWhiteSpace(_cfg["Twilio:MessagingServiceSid"]));

    public bool IsAvailable => Configured || _env.IsDevelopment();

    public async Task<bool> SendAsync(string toE164, string message, CancellationToken ct = default)
    {
        if (!Configured)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("SMS (dev, not sent) to {To}: {Message}", toE164, message);
                return true;
            }
            _logger.LogError("SMS requested but Twilio is not configured.");
            return false;
        }

        var sid = _cfg["Twilio:AccountSid"]!;
        var fields = new List<KeyValuePair<string, string>>
        {
            new("To", toE164),
            new("Body", message),
        };
        var service = _cfg["Twilio:MessagingServiceSid"];
        if (!string.IsNullOrWhiteSpace(service)) fields.Add(new("MessagingServiceSid", service));
        else fields.Add(new("From", _cfg["Twilio:From"]!));

        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(fields),
        };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{_cfg["Twilio:AuthToken"]}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return true;
            _logger.LogError("Twilio SMS failed: {Status}", (int)resp.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Twilio SMS request threw.");
            return false;
        }
    }
}
