using Zonar.Api.Contracts;
using Zonar.Api.Domain;

namespace Zonar.Api.Services.Scoring;

public record ScoreResult(int Score, QualityLabel Label, string ScorerUsed, IReadOnlyList<ScoreReason> Reasons)
{
    public bool IsSpam => Label == QualityLabel.Spam;

    public ScoreResponse ToResponse() => new(Score, Label, ScorerUsed, Reasons);

    /// <summary>Maps a 0–100 score to a label (spam is decided separately by the rule engine).</summary>
    public static QualityLabel LabelFor(int score) => score switch
    {
        >= 70 => QualityLabel.High,
        >= 40 => QualityLabel.Medium,
        _ => QualityLabel.Low
    };
}
