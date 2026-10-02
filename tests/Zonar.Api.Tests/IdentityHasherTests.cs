using Zonar.Api.Options;
using Zonar.Api.Services.Privacy;
using Zonar.Api.Telegram;
using static Zonar.Api.Tests.TestHelpers;

namespace Zonar.Api.Tests;

public class IdentityHasherTests
{
    private static IdentityHasher Hasher(string salt) => new(Opt(new PrivacyOptions { HashSalt = salt }));

    [Fact]
    public void Same_identity_gives_same_hash_regardless_of_case()
        => Assert.Equal(Hasher("s").HashIdentity("telegram:123"), Hasher("s").HashIdentity("TELEGRAM:123 "));

    [Fact]
    public void Different_salt_gives_different_hash()
        => Assert.NotEqual(Hasher("a").HashIdentity("telegram:123"), Hasher("b").HashIdentity("telegram:123"));

    [Fact]
    public void Hash_is_hex_sha256_and_hides_the_raw_identity()
    {
        var hash = Hasher("s").HashIdentity("telegram:123456789");
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain("123456789", hash);
    }

    [Fact]
    public void Content_hash_ignores_case_and_extra_whitespace()
        => Assert.Equal(IdentityHasher.HashContent("Hello   World"), IdentityHasher.HashContent(" hello world "));

    [Fact]
    public void Pseudonym_has_expected_format()
        => Assert.Matches("^Contributor-[0-9A-F]{6}$", IdentityHasher.Pseudonym(Hasher("s").HashIdentity("x")));

    [Theory]
    [InlineData("/score hello world", "ZonarBot", "score", "hello world")]
    [InlineData("/score@ZonarBot hi", "ZonarBot", "score", "hi")]
    [InlineData("/TOP", "ZonarBot", "top", "")]
    public void Commands_are_parsed(string text, string bot, string command, string args)
    {
        var (c, a) = TelegramUpdateHandler.ParseCommand(text, bot);
        Assert.Equal(command, c);
        Assert.Equal(args, a);
    }

    [Fact]
    public void Commands_for_other_bots_are_ignored()
        => Assert.Null(TelegramUpdateHandler.ParseCommand("/stats@OtherBot", "ZonarBot").Command);
}
