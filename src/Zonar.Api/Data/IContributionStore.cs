using Zonar.Api.Contracts;
using Zonar.Api.Domain;

namespace Zonar.Api.Data;

/// <summary>All database access goes through this interface (keeps services testable).</summary>
public interface IContributionStore
{
    Task<Community> GetOrCreateCommunityAsync(string externalId, string name, ContributionSource source, CancellationToken ct);
    Task<Community?> FindCommunityAsync(string externalId, CancellationToken ct);
    Task<Contributor> GetOrCreateContributorAsync(string identityHash, string displayName, ContributionSource source, CancellationToken ct);
    Task<Contributor?> FindContributorByHashAsync(string identityHash, CancellationToken ct);

    Task<bool> IsDuplicateAsync(int contributorId, string contentHash, DateTime sinceUtc, CancellationToken ct);
    Task<int> GetPointsEarnedSinceAsync(int contributorId, DateTime sinceUtc, CancellationToken ct);
    Task AddContributionAsync(Contribution contribution, CancellationToken ct);

    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(int? communityId, DateTime sinceUtc, int top, CancellationToken ct);
    Task<ContributorProfile?> GetContributorProfileAsync(int contributorId, CancellationToken ct);
    Task<IReadOnlyList<CommunitySummary>> GetCommunitiesAsync(CancellationToken ct);
    Task<PlatformStats> GetStatsAsync(DateTime last24hUtc, string scoringEngine, CancellationToken ct);
    Task<bool> AnyContributionsAsync(CancellationToken ct);
}
