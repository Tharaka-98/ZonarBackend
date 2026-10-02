using Microsoft.EntityFrameworkCore;
using Zonar.Api.Contracts;
using Zonar.Api.Domain;

namespace Zonar.Api.Data;

public class EfContributionStore : IContributionStore
{
    private readonly ZonarDbContext _db;
    private readonly TimeProvider _clock;

    public EfContributionStore(ZonarDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Community> GetOrCreateCommunityAsync(string externalId, string name, ContributionSource source, CancellationToken ct)
    {
        var community = await _db.Communities.FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);
        if (community is not null)
        {
            if (community.Name != name && !string.IsNullOrWhiteSpace(name)) community.Name = name;
            return community;
        }

        community = new Community { ExternalId = externalId, Name = name, Source = source, CreatedAtUtc = Now };
        _db.Communities.Add(community);
        await _db.SaveChangesAsync(ct);
        return community;
    }

    public Task<Community?> FindCommunityAsync(string externalId, CancellationToken ct)
        => _db.Communities.AsNoTracking().FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);

    public async Task<Contributor> GetOrCreateContributorAsync(string identityHash, string displayName, ContributionSource source, CancellationToken ct)
    {
        var contributor = await _db.Contributors.FirstOrDefaultAsync(c => c.IdentityHash == identityHash, ct);
        if (contributor is not null) return contributor;

        contributor = new Contributor
        {
            IdentityHash = identityHash,
            DisplayName = displayName,
            Source = source,
            CreatedAtUtc = Now,
            LastActiveAtUtc = Now
        };
        _db.Contributors.Add(contributor);
        await _db.SaveChangesAsync(ct);
        return contributor;
    }

    public Task<Contributor?> FindContributorByHashAsync(string identityHash, CancellationToken ct)
        => _db.Contributors.AsNoTracking().FirstOrDefaultAsync(c => c.IdentityHash == identityHash, ct);

    public Task<bool> IsDuplicateAsync(int contributorId, string contentHash, DateTime sinceUtc, CancellationToken ct)
        => _db.Contributions.AnyAsync(c =>
            c.ContributorId == contributorId && c.ContentHash == contentHash && c.CreatedAtUtc >= sinceUtc, ct);

    public async Task<int> GetPointsEarnedSinceAsync(int contributorId, DateTime sinceUtc, CancellationToken ct)
        => await _db.Contributions
            .Where(c => c.ContributorId == contributorId && c.CreatedAtUtc >= sinceUtc)
            .SumAsync(c => c.RewardPoints, ct);

    public async Task AddContributionAsync(Contribution contribution, CancellationToken ct)
    {
        var contributor = await _db.Contributors.FirstAsync(c => c.Id == contribution.ContributorId, ct);
        contributor.LastActiveAtUtc = contribution.CreatedAtUtc;

        _db.Contributions.Add(contribution);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(int? communityId, DateTime sinceUtc, int top, CancellationToken ct)
    {
        var query = _db.Contributions.AsNoTracking().Where(c => c.CreatedAtUtc >= sinceUtc);
        if (communityId is not null) query = query.Where(c => c.CommunityId == communityId);

        var rows = await query
            .GroupBy(c => c.ContributorId)
            .Select(g => new
            {
                ContributorId = g.Key,
                Points = g.Sum(x => x.RewardPoints),
                Count = g.Count(),
                Avg = g.Average(x => (double)x.Score)
            })
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.Avg)
            .Take(top)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.ContributorId).ToList();
        var names = await _db.Contributors.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, ct);

        return rows.Select((r, i) => new LeaderboardEntry(
                i + 1, r.ContributorId, names.GetValueOrDefault(r.ContributorId, "Unknown"),
                r.Points, r.Count, Math.Round(r.Avg, 1)))
            .ToList();
    }

    public async Task<ContributorProfile?> GetContributorProfileAsync(int contributorId, CancellationToken ct)
    {
        var c = await _db.Contributors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == contributorId, ct);
        if (c is null) return null;

        var mine = _db.Contributions.AsNoTracking().Where(x => x.ContributorId == contributorId);

        var total = await mine.SumAsync(x => x.RewardPoints, ct);
        var count = await mine.CountAsync(ct);
        var avg = await mine.AverageAsync(x => (double?)x.Score, ct) ?? 0;
        var high = await mine.CountAsync(x => x.Label == QualityLabel.High, ct);
        var spam = await mine.CountAsync(x => x.Label == QualityLabel.Spam, ct);
        var recent = await mine
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => new RecentContribution(x.Score, x.Label, x.RewardPoints, x.CreatedAtUtc))
            .ToListAsync(ct);

        return new ContributorProfile(c.Id, c.DisplayName, c.Source, total, count, Math.Round(avg, 1),
            high, spam, c.CreatedAtUtc, c.LastActiveAtUtc, recent);
    }

    public async Task<IReadOnlyList<CommunitySummary>> GetCommunitiesAsync(CancellationToken ct)
    {
        var communities = await _db.Communities.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);

        var totals = await _db.Contributions.AsNoTracking()
            .GroupBy(c => c.CommunityId)
            .Select(g => new { CommunityId = g.Key, Count = g.Count(), Points = g.Sum(x => x.RewardPoints) })
            .ToDictionaryAsync(x => x.CommunityId, ct);

        var pairs = await _db.Contributions.AsNoTracking()
            .Select(c => new { c.CommunityId, c.ContributorId })
            .Distinct()
            .ToListAsync(ct);
        var people = pairs.GroupBy(p => p.CommunityId).ToDictionary(g => g.Key, g => g.Count());

        return communities.Select(c => new CommunitySummary(
                c.Id, c.Name, c.Source,
                people.GetValueOrDefault(c.Id),
                totals.TryGetValue(c.Id, out var t) ? t.Count : 0,
                totals.TryGetValue(c.Id, out var t2) ? t2.Points : 0))
            .ToList();
    }

    public async Task<PlatformStats> GetStatsAsync(DateTime last24hUtc, string scoringEngine, CancellationToken ct)
    {
        var all = _db.Contributions.AsNoTracking();

        return new PlatformStats(
            TotalContributions: await all.CountAsync(ct),
            TotalContributors: await _db.Contributors.CountAsync(ct),
            TotalCommunities: await _db.Communities.CountAsync(ct),
            TotalPointsAwarded: await all.SumAsync(c => c.RewardPoints, ct),
            AverageScore: Math.Round(await all.AverageAsync(c => (double?)c.Score, ct) ?? 0, 1),
            SpamBlocked: await all.CountAsync(c => c.Label == QualityLabel.Spam, ct),
            DuplicatesBlocked: await all.CountAsync(c => c.IsDuplicate, ct),
            ContributionsLast24h: await all.CountAsync(c => c.CreatedAtUtc >= last24hUtc, ct),
            ScoringEngine: scoringEngine);
    }

    public Task<bool> AnyContributionsAsync(CancellationToken ct) => _db.Contributions.AnyAsync(ct);
}
