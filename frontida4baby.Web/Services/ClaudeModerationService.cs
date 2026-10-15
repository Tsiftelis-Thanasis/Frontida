using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using frontida4baby.Web.Models.Entities;
using Microsoft.Extensions.Options;

namespace frontida4baby.Web.Services;

/// <summary>
/// Stage-2 moderation: semantic analysis via the Claude API (Haiku model).
/// Handles disguised abuse, coded language, context-aware decisions.
/// </summary>
public class ClaudeModerationService
{
    private static int _consecutiveFallbacks;
    private static DateTime _lastFallbackAlertUtc = DateTime.MinValue;
    private static readonly TimeSpan FallbackAlertInterval = TimeSpan.FromMinutes(60);

    private readonly HttpClient _http;
    private readonly ModerationOptions _opts;
    private readonly ILogger<ClaudeModerationService> _logger;
    private readonly IAppLogService _appLog;
    private readonly INotificationService _notifications;

    private const string SystemPrompt = """
        You are a content moderation system for frontida4baby, a Greek childcare and
        caregiving platform that connects families with verified caregivers.

        Evaluate whether the submitted post or reply is safe to publish.
        Content may be written in Greek, English, or both.

        REJECT if the content contains ANY of:
        - Profanity, insults, or offensive language in any language
        - Threats, intimidation, or violent language toward any person or group
        - Hate speech based on ethnicity, religion, gender, sexual orientation, or disability
        - Illegal offers: drugs, weapons, counterfeit goods, unlicensed medical services
        - Sexual solicitation or explicit content
        - Spam or commercial advertising unrelated to caregiving
        - Requests to share personal contact information to bypass the platform

        MARK AS PENDING REVIEW (uncertain) if:
        - Language is ambiguous and you cannot determine intent confidently
        - Content uses coded or euphemistic language that may be harmful
        - Confidence is below 0.75

        APPROVE if none of the above apply. Normal caregiving requests, caregiver
        introductions, experience descriptions, availability, rate discussions,
        and follow-up questions are always acceptable.

        Respond ONLY with valid JSON — no markdown, no text outside the object:
        {"decision":"Approved","reason":null,"category":null,"confidence":0.98}
        {"decision":"Rejected","reason":"Short human-readable reason (max 80 chars)","category":"Profanity","confidence":0.99}
        {"decision":"PendingReview","reason":"Ambiguous language","category":null,"confidence":0.60}

        Valid category values: Profanity, Threat, HateSpeech, IllegalOffer, SexualContent, Spam, ContactInfo, null
        """;

    public ClaudeModerationService(
        HttpClient http,
        IOptions<ModerationOptions> opts,
        ILogger<ClaudeModerationService> logger,
        IAppLogService appLog,
        INotificationService notifications)
    {
        _opts = opts.Value;
        _http = http;
        _logger = logger;
        _appLog = appLog;
        _notifications = notifications;
        _http.BaseAddress ??= new Uri("https://api.anthropic.com");
        if (!_http.DefaultRequestHeaders.Contains("x-api-key"))
            _http.DefaultRequestHeaders.Add("x-api-key", _opts.ClaudeApiKey);
        if (!_http.DefaultRequestHeaders.Contains("anthropic-version"))
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<ModerationResult> CheckAsync(string? title, string body)
    {
        if (string.IsNullOrWhiteSpace(_opts.ClaudeApiKey))
            return await FallbackAsync("API key not configured.");

        var userContent = title is null
            ? $"REPLY:\n{body}"
            : $"TITLE: {title}\n\nBODY:\n{body}";

        var requestBody = new ClaudeRequest(
            Model: _opts.ClaudeModel,
            MaxTokens: _opts.MaxTokens,
            System: SystemPrompt,
            Messages: [new ClaudeMessage("user", userContent)]
        );

        var json = JsonSerializer.Serialize(requestBody, _jsonOpts);

        try
        {
            using var response = await _http.PostAsync(
                "/v1/messages",
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return await FallbackAsync(
                    $"Claude API error: {(int)response.StatusCode} {response.StatusCode} — {ExtractErrorMessage(errorBody)}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, _jsonOpts);
            var text = claudeResponse?.Content?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
                return await FallbackAsync("Empty response from Claude.");

            // Strip markdown fences if Claude wraps the JSON
            var jsonText = ExtractJson(text);

            ModerationDecision? decision;
            try
            {
                decision = JsonSerializer.Deserialize<ModerationDecision>(jsonText, _jsonOpts);
            }
            catch (JsonException)
            {
                return await FallbackAsync($"Could not parse Claude response: {text[..Math.Min(text.Length, 200)]}");
            }

            if (decision is null)
                return await FallbackAsync("Could not parse Claude response.");

            var status = decision.Decision switch
            {
                "Approved"      => ModerationStatus.Approved,
                "Rejected"      => ModerationStatus.Rejected,
                _               => ModerationStatus.PendingReview,
            };

            Interlocked.Exchange(ref _consecutiveFallbacks, 0);
            return new ModerationResult(status, decision.Reason, decision.Category,
                ModerationStage.Claude, decision.Confidence);
        }
        catch (TaskCanceledException)
        {
            return await FallbackAsync("Claude API timed out.");
        }
        catch (Exception ex)
        {
            return await FallbackAsync($"Claude API unavailable: {ex.GetType().Name}");
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? body;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return body[..Math.Min(body.Length, 200)];
        }
    }

    private async Task<ModerationResult> FallbackAsync(string reason)
    {
        var count = Interlocked.Increment(ref _consecutiveFallbacks);
        _logger.LogWarning("Claude moderation fallback #{Count}: {Reason}", count, reason);

        try
        {
            await _appLog.LogAsync(AppLogLevel.Error, "Moderation",
                $"Claude moderation failed: {reason}",
                details: $"Consecutive failures: {count}. Fallback policy: {_opts.FallbackOnTimeout}.");

            var now = DateTime.UtcNow;
            if (count == 1 || now - _lastFallbackAlertUtc >= FallbackAlertInterval)
            {
                _lastFallbackAlertUtc = now;
                await _notifications.NotifyModerationDegradedAsync(reason, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record or announce Claude moderation failure.");
        }

        var status = _opts.FallbackOnTimeout switch
        {
            "Approved" => ModerationStatus.Approved,
            "Rejected" => ModerationStatus.Rejected,
            _          => ModerationStatus.PendingReview,
        };
        return new ModerationResult(status, reason, null, ModerationStage.Claude, null);
    }

    private static string ExtractJson(string text)
    {
        // Strip ```json ... ``` fences
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];
            trimmed = trimmed.Trim();
        }

        // Extract the first JSON object if surrounded by other text
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return trimmed;
    }

    // ── JSON serialisation helpers ────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private record ClaudeRequest(
        string Model,
        int MaxTokens,
        string System,
        ClaudeMessage[] Messages);

    private record ClaudeMessage(string Role, string Content);

    private record ClaudeResponse(ClaudeContent[]? Content);

    private record ClaudeContent(string? Type, string? Text);

    private record ModerationDecision(
        string Decision,
        string? Reason,
        string? Category,
        float Confidence);
}
