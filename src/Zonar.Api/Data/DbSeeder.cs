using Zonar.Api.Domain;
using Zonar.Api.Services;

namespace Zonar.Api.Data;

/// <summary>Fills an empty database with a few demo contributors so the leaderboard is not empty on demo day.</summary>
public static class DbSeeder
{
    private static readonly (string Name, string Message)[] Samples =
    {
        ("Nadia", "Volume dropped 40% this week while price held support at 0.12, which means sellers are exhausted. I'd watch for a breakout above resistance before adding risk."),
        ("Nadia", "Has anyone compared the sentiment data from the last two AMAs? I think the community reacted better to the roadmap update than to the audit news."),
        ("Liam", "Good point about liquidity. However, the chart only covers 7 days, so the trend could still reverse if the market turns."),
        ("Liam", "What metrics does the model use to rate messages? Knowing that would help us write better analysis."),
        ("Priya", "I tested the bot in our study group – it rewarded detailed questions and ignored one-word replies. Nice anti-spam design."),
        ("Priya", "gm"),
        ("Ken", "AIRDROP!!! FREE MONEY dm me now 100x guaranteed"),
        ("Ken", "Price is going up because more people are joining the community and talking about the data."),
        ("Ava", "Security tip: never share your seed phrase. Real admins will never DM you first."),
    };

    public static async Task SeedAsync(ContributionService service, CancellationToken ct = default)
    {
        foreach (var (name, message) in Samples)
        {
            await service.RecordAsync(new ContributionInput(
                CommunityExternalId: "demo",
                CommunityName: "Zonar Demo Community",
                Source: ContributionSource.Seed,
                IdentityKey: $"seed:{name}",
                DisplayName: name,
                Text: message), ct);
        }
    }
}
