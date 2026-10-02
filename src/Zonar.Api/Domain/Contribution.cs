namespace Zonar.Api.Domain;

/// <summary>
/// One scored message. The message text itself is NOT stored – only a hash
/// (for duplicate detection), its length and the scoring outcome.
/// </summary>
public class Contribution
{
    public long Id { get; set; }

    public int CommunityId { get; set; }
    public Community? Community { get; set; }

    public int ContributorId { get; set; }
    public Contributor? Contributor { get; set; }

    public string ContentHash { get; set; } = "";
    public int Length { get; set; }

    public int Score { get; set; }
    public QualityLabel Label { get; set; }
    public bool IsDuplicate { get; set; }
    public string ScorerUsed { get; set; } = "";

    public int RewardPoints { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
