using Microsoft.Extensions.Options;
using Zonar.Api.Contracts;
using Zonar.Api.Domain;
using Zonar.Api.Options;
using Zonar.Api.Services.Scoring;

namespace Zonar.Api.Tests;

internal static class TestHelpers
{
    public static IOptions<T> Opt<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);

    public static ScoreResult Score(int score, QualityLabel? label = null) =>
        new(score, label ?? ScoreResult.LabelFor(score), "test", Array.Empty<ScoreReason>());

    public static RewardOptions DefaultRewards() => new();
}
