namespace Zonar.Api.Domain;

/// <summary>A group where contributions happen, e.g. a Telegram group or the website demo.</summary>
public class Community
{
    public int Id { get; set; }

    /// <summary>Stable external key, e.g. "telegram:-1001234567890" or "web".</summary>
    public string ExternalId { get; set; } = "";

    public string Name { get; set; } = "";
    public ContributionSource Source { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<Contribution> Contributions { get; set; } = new();
}
