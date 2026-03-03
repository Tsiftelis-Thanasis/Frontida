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
    private readonly HttpClient _http;
    private readonly ModerationOptions _opts;

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

    public ClaudeModerationService(HttpClient http, IOptions<ModerationOptions> opts)
    {
        _opts = opts.Value;
        _http = http;
        _http.BaseAddress = new Uri("https://api.anthropic.com");
        _http.DefaultRequestHeaders.Add("x-api-key", _opts.ClaudeApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _http.Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
    }

    public async Task<ModerationResult> CheckAsync(string? title, string body)
    {
        if (string.IsNullOrWhiteSpace(_opts.ClaudeApiKey))
            return Fallback("API key not configured.");

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
                return Fallback($"Claude API error: {response.StatusCode}");

            var responseJson = await response.Content.ReadAsStringAsync();
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, _jsonOpts);
            var text = claudeResponse?.Content?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
                return Fallback("Empty response from Claude.");

            var decision = JsonSerializer.Deserialize<ModerationDecision>(text, _jsonOpts);
            if (decision is null)
                return Fallback("Could not parse Claude response.");

            var status = decision.Decision switch
            {
                "Approved"      => ModerationStatus.Approved,
                "Rejected"      => ModerationStatus.Rejected,
                _               => ModerationStatus.PendingReview,
            };

            return new ModerationResult(status, decision.Reason, decision.Category,
                ModerationStage.Claude, decision.Confidence);
        }
        catch (TaskCanceledException)
        {
            return Fallback("Claude API timed out.");
        }
        catch
        {
            return Fallback("Claude API unavailable.");
        }
    }

    private ModerationResult Fallback(string reason)
    {
        var status = _opts.FallbackOnTimeout switch
        {
            "Approved" => ModerationStatus.Approved,
            "Rejected" => ModerationStatus.Rejected,
            _          => ModerationStatus.PendingReview,
        };
        return new ModerationResult(status, reason, null, ModerationStage.Claude, null);
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
