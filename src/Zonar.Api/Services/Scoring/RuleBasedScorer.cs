using System.Text.RegularExpressions;
using Zonar.Api.Contracts;
using Zonar.Api.Domain;

namespace Zonar.Api.Services.Scoring;

/// <summary>
/// Transparent, explainable scorer. Every rule adds or removes points and records
/// a human-readable reason, so users can see exactly why they got their score.
/// </summary>
public partial class RuleBasedScorer : IMessageScorer
{
    public const string Name = "rules";

    private static readonly string[] SpamPhrases =
    {
        "airdrop", "giveaway", "free money", "dm me", "send me", "100x", "1000x",
        "guaranteed", "double your", "click here", "join now", "whatsapp me", "pump it"
    };

    private static readonly string[] ReasoningWords =
    {
        "because", "therefore", "however", "since", "which means", "for example",
        "compared", "although", "so that", "as a result"
    };

    private static readonly string[] TopicWords =
    {
        "liquidity", "volume", "support", "resistance", "trend", "risk", "market",
        "analysis", "data", "chart", "sentiment", "strategy", "price", "model",
        "research", "community", "roadmap", "security", "audit", "ai"
    };

    public Task<ScoreResult> ScoreAsync(string message, CancellationToken ct = default)
        => Task.FromResult(Score(message));

    public ScoreResult Score(string message)
    {
        var text = (message ?? "").Trim();
        var reasons = new List<ScoreReason>();

        if (text.Length == 0)
            return new ScoreResult(0, QualityLabel.Spam, Name, new[] { new ScoreReason("Empty message", -50) });

        var score = 50;
        void Apply(int impact, string reason)
        {
            score += impact;
            reasons.Add(new ScoreReason(reason, impact));
        }

        var lower = text.ToLowerInvariant();
        var words = WordRegex().Matches(lower).Select(m => m.Value).ToArray();
        var letters = text.Count(char.IsLetter);

        // 1. Content present at all?
        if (letters == 0)
        {
            Apply(-35, "No words – only emojis or symbols");
        }

        // 2. Length
        switch (words.Length)
        {
            case < 3: Apply(-25, "Very short message"); break;
            case < 8: Apply(-5, "Short message"); break;
            case >= 12 and <= 120: Apply(12, "Detailed message"); break;
            case > 200: Apply(-5, "Very long – may be a wall of text"); break;
        }

        // 3. Vocabulary variety (catches copy-paste / repeated words)
        if (words.Length >= 8)
        {
            var diversity = words.Distinct().Count() / (double)words.Length;
            if (diversity < 0.4) Apply(-25, "Repetitive wording");
            else if (diversity > 0.75) Apply(8, "Varied vocabulary");
        }

        // 4. Formatting abuse
        if (StretchedRegex().IsMatch(lower)) Apply(-10, "Stretched characters (e.g. 'sooooo')");
        if (PunctuationRegex().IsMatch(text)) Apply(-8, "Excessive punctuation");
        if (letters >= 10 && text.Count(char.IsUpper) / (double)letters > 0.6) Apply(-15, "Mostly CAPITAL letters");

        // 5. Links and mentions
        var links = LinkRegex().Matches(lower).Count;
        if (links == 1) Apply(-5, "Contains a link");
        else if (links >= 2) Apply(-20, "Multiple links");
        if (MentionRegex().Matches(text).Count >= 3) Apply(-10, "Mass-mentions other users");

        // 6. Spam / scam phrases
        var spamHits = SpamPhrases.Where(lower.Contains).Take(2).ToList();
        foreach (var hit in spamHits) Apply(-20, $"Promotional phrase: \"{hit}\"");

        // 7. Positive signals
        if (text.Contains('?') && words.Length >= 5) Apply(8, "Asks a meaningful question");
        if (ReasoningWords.Any(lower.Contains)) Apply(10, "Explains reasoning");

        var topicHits = TopicWords.Count(t => words.Contains(t));
        if (topicHits >= 2) Apply(10, "Relevant, on-topic insight");
        else if (topicHits == 1) Apply(5, "On-topic");

        if (NumberRegex().IsMatch(text) && words.Length >= 8 && spamHits.Count == 0) Apply(6, "Includes concrete numbers or data");

        score = Math.Clamp(score, 0, 100);

        var label = spamHits.Count > 0 || score < 20
            ? QualityLabel.Spam
            : ScoreResult.LabelFor(score);

        return new ScoreResult(score, label, Name, reasons);
    }

    [GeneratedRegex(@"[\p{L}\p{N}']+")] private static partial Regex WordRegex();
    [GeneratedRegex(@"(\p{L})\1{4,}")] private static partial Regex StretchedRegex();
    [GeneratedRegex(@"[!?]{3,}")] private static partial Regex PunctuationRegex();
    [GeneratedRegex(@"https?://|www\.|t\.me/")] private static partial Regex LinkRegex();
    [GeneratedRegex(@"(^|\s)@\w+")] private static partial Regex MentionRegex();
    [GeneratedRegex(@"\d")] private static partial Regex NumberRegex();
}
