namespace Zonar.Api.Options;

public class PrivacyOptions
{
    public const string Section = "Privacy";

    /// <summary>Secret used to HMAC user identities. Change it in production and keep it out of git.</summary>
    public string HashSalt { get; set; } = "change-me-in-production";
}
