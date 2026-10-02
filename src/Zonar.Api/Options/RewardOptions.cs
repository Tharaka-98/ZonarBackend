namespace Zonar.Api.Options;

public class RewardOptions
{
    public const string Section = "Rewards";

    public int HighThreshold { get; set; } = 70;
    public int MediumThreshold { get; set; } = 40;
    public int HighReward { get; set; } = 10;
    public int MediumReward { get; set; } = 3;

    /// <summary>Max points one contributor can earn in a rolling 24h window (anti-farming).</summary>
    public int DailyCap { get; set; } = 50;

    /// <summary>The same message from the same person inside this window earns nothing.</summary>
    public int DuplicateWindowHours { get; set; } = 24;
}
