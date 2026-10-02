using Zonar.Api.Domain;
using Zonar.Api.Services.Rewards;
using static Zonar.Api.Tests.TestHelpers;

namespace Zonar.Api.Tests;

public class RewardPolicyTests
{
    private readonly RewardPolicy _policy = new(Opt(DefaultRewards()));

    [Fact]
    public void Spam_earns_nothing()
        => Assert.Equal(0, _policy.Decide(Score(90, QualityLabel.Spam), false, 0).Points);

    [Fact]
    public void Duplicate_earns_nothing()
        => Assert.Equal(0, _policy.Decide(Score(90), true, 0).Points);

    [Theory]
    [InlineData(85, 10)]
    [InlineData(70, 10)]
    [InlineData(55, 3)]
    [InlineData(40, 3)]
    [InlineData(39, 0)]
    public void Points_follow_score_thresholds(int score, int expected)
        => Assert.Equal(expected, _policy.Decide(Score(score), false, 0).Points);

    [Fact]
    public void Daily_cap_blocks_further_rewards()
    {
        var d = _policy.Decide(Score(90), false, 50);
        Assert.Equal(0, d.Points);
        Assert.True(d.DailyCapReached);
    }

    [Fact]
    public void Reward_is_trimmed_to_remaining_allowance()
    {
        var d = _policy.Decide(Score(90), false, 45);
        Assert.Equal(5, d.Points);
        Assert.True(d.DailyCapReached);
    }
}
