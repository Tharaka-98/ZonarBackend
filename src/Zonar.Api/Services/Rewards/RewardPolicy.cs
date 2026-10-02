using Microsoft.Extensions.Options;
using Zonar.Api.Options;
using Zonar.Api.Services.Scoring;

namespace Zonar.Api.Services.Rewards;

public record RewardDecision(int Points, bool DailyCapReached, string Explanation);

/// <summary>Decides how many (simulated) reward points a scored message earns.</summary>
public class RewardPolicy
{
    private readonly RewardOptions _o;

    public RewardPolicy(IOptions<RewardOptions> options) => _o = options.Value;

    public RewardDecision Decide(ScoreResult score, bool isDuplicate, int pointsEarnedToday)
    {
        if (score.IsSpam)
            return new(0, false, "Flagged as spam – no reward.");

        if (isDuplicate)
            return new(0, false, $"Same message already sent in the last {_o.DuplicateWindowHours}h – no reward.");

        var basePoints = score.Score >= _o.HighThreshold ? _o.HighReward
                       : score.Score >= _o.MediumThreshold ? _o.MediumReward
                       : 0;

        if (basePoints == 0)
            return new(0, false, "Score too low for a reward – add more detail or reasoning.");

        var remaining = Math.Max(0, _o.DailyCap - pointsEarnedToday);
        if (remaining == 0)
            return new(0, true, $"Daily cap of {_o.DailyCap} points reached – come back tomorrow.");

        var points = Math.Min(basePoints, remaining);
        return new(points, points < basePoints, $"Earned {points} points.");
    }
}
