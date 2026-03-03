using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

/// <summary>
/// Stage-1 moderation: instant offline check against banned word/phrase lists.
/// Runs in &lt;1 ms with no network calls.
/// </summary>
public class WordlistModerationService
{
    // ── Leet-speak normalisation map ─────────────────────────────────────────
    private static readonly Dictionary<char, char> LeetMap = new()
    {
        ['@'] = 'a', ['4'] = 'a',
        ['3'] = 'e',
        ['1'] = 'i', ['!'] = 'i',
        ['0'] = 'o',
        ['5'] = 's', ['$'] = 's',
        ['7'] = 't',
    };

    // ── English banned patterns ───────────────────────────────────────────────
    // Whole-word profanity
    private static readonly string[] EnglishProfanity =
    [
        "fuck", "fucking", "fucker", "motherfucker",
        "shit", "shitting",
        "cunt", "cunts",
        "bitch", "bitches",
        "asshole", "assholes",
        "bastard", "bastards",
        "cock", "dick", "pussy",
        "whore", "slut",
        "nigger", "nigga", "faggot",
    ];

    // Threat phrases (substring match, no word boundary needed)
    private static readonly string[] EnglishThreats =
    [
        "i will kill you", "i'll kill you", "i'm going to kill",
        "i will hurt you", "i'll hurt you",
        "i will find you", "you will regret",
        "bomb threat", "i will rape",
    ];

    // Illegal-offer keywords (whole-word)
    private static readonly string[] EnglishIllegal =
    [
        "cocaine", "heroin", "methamphetamine", "meth", "fentanyl",
        "buy drugs", "sell drugs", "drug dealer",
        "buy weed", "sell weed", "buy cannabis", "sell cannabis",
        "gun for sale", "weapon for sale", "buy weapon", "sell weapon",
        "buy ammo", "sell ammo",
        "fake passport", "fake documents", "counterfeit",
        "child porn", "cp for sale",
    ];

    // ── Greek banned patterns (Unicode + common Latin transcription) ──────────
    private static readonly string[] GreekProfanity =
    [
        // Greek script (tonos stripped during normalisation)
        "μαλακας", "μαλακια", "μαλακες",
        "αρχιδι", "αρχιδια",
        "γαμωτο", "γαμησου", "γαμα",
        "σκατα", "σκατο",
        "πουτανα", "πουτανες",
        "κωλοτρυπιδα", "κωλος",
        "πεος", "αιδοιο",
        "βλακας", "βλακεια",
        "μπουνιεσ", "ηλιθιος",
        // common Latin transcriptions
        "malaka", "malakas", "malakia",
        "gamoto", "gamisou",
        "putana", "poutana",
        "skata",
    ];

    private static readonly string[] GreekThreats =
    [
        "θα σε σκοτωσω", "θα σε σκοτωσω",
        "θα σε βρω", "θα σε βλαψω",
        "θα σε κανω", "θα σε χτυπησω",
    ];

    private static readonly string[] GreekIllegal =
    [
        "ναρκωτικα προς πωληση", "πουλαω ναρκωτικα",
        "αγοραζω ναρκωτικα", "οπλο προς πωληση",
        "πλαστα εγγραφα", "παραχαραξη",
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public ModerationResult Check(string? title, string body)
    {
        var combined = Normalise($"{title}\n\n{body}");

        // 1. Whole-word profanity checks
        foreach (var word in EnglishProfanity.Concat(GreekProfanity))
        {
            if (ContainsWord(combined, word))
                return Rejected("Profanity or offensive language detected.", "Profanity");
        }

        // 2. Threat phrases (substring)
        foreach (var phrase in EnglishThreats.Concat(GreekThreats))
        {
            if (combined.Contains(phrase, StringComparison.Ordinal))
                return Rejected("Threatening or violent language detected.", "Threat");
        }

        // 3. Illegal offer phrases (substring)
        foreach (var phrase in EnglishIllegal.Concat(GreekIllegal))
        {
            if (combined.Contains(phrase, StringComparison.Ordinal))
                return Rejected("Content appears to advertise illegal goods or services.", "IllegalOffer");
        }

        return new ModerationResult(ModerationStatus.Approved, null, null, ModerationStage.Wordlist, 1f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ModerationResult Rejected(string reason, string category) =>
        new(ModerationStatus.Rejected, reason, category, ModerationStage.Wordlist, 1f);

    /// <summary>
    /// Lowercases, strips diacritics (handles Greek tonos), applies leet-speak map.
    /// </summary>
    private static string Normalise(string input)
    {
        // 1. Decompose Unicode so diacritics become separate characters
        var decomposed = input.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue; // strip accent/tonos

            var lower = char.ToLowerInvariant(ch);
            sb.Append(LeetMap.TryGetValue(lower, out var mapped) ? mapped : lower);
        }

        return sb.ToString();
    }

    private static bool ContainsWord(string text, string word)
    {
        // Use word-boundary regex so "classic" doesn't match "ass"
        var pattern = $@"(?<![a-zα-ωά-ώ]){Regex.Escape(word)}(?![a-zα-ωά-ώ])";
        return Regex.IsMatch(text, pattern);
    }
}
