using Zonar.Api.Domain;
using Zonar.Api.Services.Scoring;

namespace Zonar.Api.Tests;

public class RuleBasedScorerTests
{
    private readonly RuleBasedScorer _scorer = new();

    [Fact]
    public void Empty_message_scores_zero_and_is_spam()
    {
        var r = _scorer.Score("   ");
        Assert.Equal(0, r.Score);
        Assert.Equal(QualityLabel.Spam, r.Label);
    }

    [Fact]
    public void Scam_phrases_are_flagged_as_spam()
    {
        var r = _scorer.Score("FREE MONEY airdrop!!! dm me for 100x");
        Assert.Equal(QualityLabel.Spam, r.Label);
        Assert.True(r.Score < 40);
    }

    [Fact]
    public void Thoughtful_analysis_scores_high()
    {
        var r = _scorer.Score(
            "Volume dropped 30% while price held support, which means sellers may be exhausted. " +
            "What does the sentiment data say about the next week?");
        Assert.Equal(QualityLabel.High, r.Label);
        Assert.True(r.Score >= 70);
    }

    [Fact]
    public void One_word_reply_scores_low()
    {
        var r = _scorer.Score("gm");
        Assert.True(r.Score < 40);
        Assert.Contains(r.Reasons, x => x.Text == "Very short message");
    }

    [Fact]
    public void Shouting_is_penalised()
    {
        var r = _scorer.Score("THIS PROJECT IS THE BEST EVER EVERYONE SHOULD JOIN");
        Assert.Contains(r.Reasons, x => x.Text == "Mostly CAPITAL letters");
    }

    [Fact]
    public void Repetitive_text_is_penalised()
    {
        var r = _scorer.Score("moon moon moon moon moon moon moon moon moon moon");
        Assert.Contains(r.Reasons, x => x.Text == "Repetitive wording");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("Check https://a.com and https://b.com now!!!")]
    [InlineData("Because the market trend shows risk, compare liquidity and volume data from 2024 and 2025?")]
    public void Score_is_always_between_0_and_100_with_reasons(string message)
    {
        var r = _scorer.Score(message);
        Assert.InRange(r.Score, 0, 100);
        Assert.NotEmpty(r.Reasons);
    }
}
