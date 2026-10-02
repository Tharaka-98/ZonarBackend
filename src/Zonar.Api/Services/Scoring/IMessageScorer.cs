namespace Zonar.Api.Services.Scoring;

/// <summary>
/// Rates how valuable a community message is (0–100).
/// Swappable: the rule engine works offline, the LLM scorer adds an AI opinion on top.
/// </summary>
public interface IMessageScorer
{
    Task<ScoreResult> ScoreAsync(string message, CancellationToken ct = default);
}
