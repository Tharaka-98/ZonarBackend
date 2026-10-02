namespace Zonar.Api.Domain;

/// <summary>
/// A pseudonymous participant. We never store a raw Telegram user id or username –
/// only a salted HMAC hash, so a leaked database cannot be linked back to people.
/// </summary>
public class Contributor
{
    public int Id { get; set; }
    public string IdentityHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ContributionSource Source { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastActiveAtUtc { get; set; }

    public List<Contribution> Contributions { get; set; } = new();
}
