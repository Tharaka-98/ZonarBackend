using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zonar.Api.Contracts;
using Zonar.Api.Options;

namespace Zonar.Api.Services.Scoring;

/// <summary>
/// Hybrid scorer: the rule engine acts as a cheap spam guard, then an LLM
/// (any OpenAI-compatible chat API) judges usefulness. Final score = weighted blend.
/// If the LLM fails or times out we fall back to the rule score – the API never breaks.
/// </summary>
public class LlmScorer : IMessageScorer
{
    public const string HttpClientName = "llm";

    private const string SystemPrompt = """
        You rate messages from an online crypto / AI community for how much value they add
        to the discussion. Reward insight, reasoning, helpful questions, data and original
        analysis. Penalise spam, hype, scams, shilling, insults and low-effort replies.
        Respond ONLY with JSON: {"score": <integer 0-100>, "reasons": [<max 3 short strings>]}
        """;

    private readonly RuleBasedScorer _rules;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ScoringOptions _options;
    private readonly ILogger<LlmScorer> _logger;

    public LlmScorer(RuleBasedScorer rules, IHttpClientFactory httpFactory,
        IOptions<ScoringOptions> options, ILogger<LlmScorer> logger)
    {
        _rules = rules;
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScoreResult> ScoreAsync(string message, CancellationToken ct = default)
    {
        var rule = _rules.Score(message);

        // Obvious spam never reaches the (paid) model.
        if (rule.IsSpam)
            return rule with { ScorerUsed = "rules (spam guard)" };

        try
        {
            var (llmScore, llmReasons) = await AskModelAsync(message, ct);
            var w = Math.Clamp(_options.LlmWeight, 0, 1);
            var final = (int)Math.Round(w * llmScore + (1 - w) * rule.Score);

            var reasons = llmReasons
                .Select(r => new ScoreReason($"AI: {r}", 0))
                .Concat(rule.Reasons)
                .ToList();

            return new ScoreResult(final, ScoreResult.LabelFor(final), $"hybrid ({_options.Model} + rules)", reasons);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "LLM scoring failed – falling back to rule engine");
            return rule with { ScorerUsed = "rules (AI unavailable)" };
        }
    }

    private async Task<(int Score, string[] Reasons)> AskModelAsync(string message, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var payload = new
        {
            model = _options.Model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = message }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                      ?? throw new InvalidOperationException("Empty model response");

        using var result = JsonDocument.Parse(content);
        var score = Math.Clamp(result.RootElement.GetProperty("score").GetInt32(), 0, 100);
        var reasons = result.RootElement.TryGetProperty("reasons", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).Take(3).ToArray()
            : Array.Empty<string>();

        return (score, reasons);
    }
}
