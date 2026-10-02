using Microsoft.Extensions.Options;
using Zonar.Api.Contracts;
using Zonar.Api.Data;
using Zonar.Api.Domain;
using Zonar.Api.Options;
using Zonar.Api.Services.Privacy;
using Zonar.Api.Services.Rewards;
using Zonar.Api.Services.Scoring;

namespace Zonar.Api.Services;

/// <summary>Everything needed to record one message, from any channel (web, Telegram, seed).</summary>
/// <param name="CommunityExternalId">e.g. "web" or "telegram:-100123"</param>
/// <param name="CommunityName">Human-readable community name.</param>
/// <param name="Source">Where the message came from.</param>
/// <param name="IdentityKey">Raw identity, e.g. "telegram:12345" – hashed before storage, never saved.</param>
/// <param name="DisplayName">Public name; null = generate a pseudonym.</param>
/// <param name="Text">The message (used in memory only, never stored).</param>
public record ContributionInput(
    string CommunityExternalId,
    string CommunityName,
    ContributionSource Source,
    string IdentityKey,
    string? DisplayName,
    string Text);

/// <summary>
/// The core use case: hash identity → detect duplicates → score → apply reward policy → save.
/// The message text is used in memory only and is never written to the database.
/// </summary>
public class ContributionService
{
    public const int MaxMessageLength = 2000;

    private readonly IContributionStore _store;
    private readonly IMessageScorer _scorer;
    private readonly RewardPolicy _rewards;
    private readonly IdentityHasher _hasher;
    private readonly RewardOptions _rewardOptions;
    private readonly TimeProvider _clock;

    public ContributionService(IContributionStore store, IMessageScorer scorer, RewardPolicy rewards,
        IdentityHasher hasher, IOptions<RewardOptions> rewardOptions, TimeProvider clock)
    {
        _store = store;
        _scorer = scorer;
        _rewards = rewards;
        _hasher = hasher;
        _rewardOptions = rewardOptions.Value;
        _clock = clock;
    }

    public Task<ScoreResult> PreviewAsync(string text, CancellationToken ct = default)
        => _scorer.ScoreAsync(Truncate(text), ct);

    public async Task<ContributionResponse> RecordAsync(ContributionInput input, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var text = Truncate(input.Text);

        var identityHash = _hasher.HashIdentity(input.IdentityKey);
        var displayName = string.IsNullOrWhiteSpace(input.DisplayName)
            ? IdentityHasher.Pseudonym(identityHash)
            : input.DisplayName.Trim();

        var community = await _store.GetOrCreateCommunityAsync(input.CommunityExternalId, input.CommunityName, input.Source, ct);
        var contributor = await _store.GetOrCreateContributorAsync(identityHash, displayName, input.Source, ct);

        var contentHash = IdentityHasher.HashContent(text);
        var isDuplicate = await _store.IsDuplicateAsync(contributor.Id, contentHash,
            now.AddHours(-_rewardOptions.DuplicateWindowHours), ct);

        var score = await _scorer.ScoreAsync(text, ct);
        var earnedToday = await _store.GetPointsEarnedSinceAsync(contributor.Id, now.AddHours(-24), ct);
        var decision = _rewards.Decide(score, isDuplicate, earnedToday);

        await _store.AddContributionAsync(new Contribution
        {
            CommunityId = community.Id,
            ContributorId = contributor.Id,
            ContentHash = contentHash,
            Length = text.Length,
            Score = score.Score,
            Label = score.Label,
            IsDuplicate = isDuplicate,
            ScorerUsed = score.ScorerUsed,
            RewardPoints = decision.Points,
            CreatedAtUtc = now
        }, ct);

        return new ContributionResponse(
            contributor.Id,
            contributor.DisplayName,
            score.ToResponse(),
            decision.Points,
            isDuplicate,
            decision.DailyCapReached,
            earnedToday + decision.Points,
            decision.Explanation);
    }

    private static string Truncate(string? text)
    {
        text = (text ?? "").Trim();
        return text.Length > MaxMessageLength ? text[..MaxMessageLength] : text;
    }
}
