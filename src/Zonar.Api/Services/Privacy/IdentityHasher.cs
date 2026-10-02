using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Zonar.Api.Options;

namespace Zonar.Api.Services.Privacy;

/// <summary>
/// Turns identities and message text into one-way hashes.
/// - Identities use HMAC-SHA256 with a secret salt (cannot be reversed or brute-forced without the salt).
/// - Message content uses SHA-256 of the normalised text (only used to spot duplicates).
/// </summary>
public class IdentityHasher
{
    private readonly byte[] _key;

    public IdentityHasher(IOptions<PrivacyOptions> options)
    {
        _key = Encoding.UTF8.GetBytes(options.Value.HashSalt);
    }

    public string HashIdentity(string identity)
        => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(identity.Trim().ToLowerInvariant())));

    public static string HashContent(string text)
    {
        var normalised = string.Join(' ', text.ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)));
    }

    /// <summary>Friendly pseudonym derived from the hash, e.g. "Contributor-7F3A2C".</summary>
    public static string Pseudonym(string identityHash) => $"Contributor-{identityHash[..6]}";
}
