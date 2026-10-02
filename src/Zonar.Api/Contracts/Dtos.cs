using Zonar.Api.Domain;

namespace Zonar.Api.Contracts;

// ---------- Requests ----------

/// <summary>Score a message without saving anything.</summary>
public record ScoreRequest(string Message);

/// <summary>Record a contribution from the website demo.</summary>
/// <param name="Nickname">Public nickname shown on the leaderboard (2–24 chars).</param>
/// <param name="Message">The message to score (never stored).</param>
public record ContributionRequest(string Nickname, string Message);

// ---------- Responses ----------

public record ScoreReason(string Text, int Impact);

public record ScoreResponse(
    int Score,
    QualityLabel Label,
    string ScorerUsed,
    IReadOnlyList<ScoreReason> Reasons);

public record ContributionResponse(
    int ContributorId,
    string DisplayName,
    ScoreResponse Result,
    int RewardPoints,
    bool IsDuplicate,
    bool DailyCapReached,
    int PointsEarnedToday,
    string Message);

public record LeaderboardEntry(
    int Rank,
    int ContributorId,
    string DisplayName,
    int TotalPoints,
    int Contributions,
    double AverageScore);

public record RecentContribution(int Score, QualityLabel Label, int RewardPoints, DateTime CreatedAtUtc);

public record ContributorProfile(
    int ContributorId,
    string DisplayName,
    ContributionSource Source,
    int TotalPoints,
    int Contributions,
    double AverageScore,
    int HighQualityCount,
    int SpamCount,
    DateTime MemberSinceUtc,
    DateTime LastActiveAtUtc,
    IReadOnlyList<RecentContribution> Recent);

public record CommunitySummary(
    int Id,
    string Name,
    ContributionSource Source,
    int Contributors,
    int Contributions,
    int TotalPoints);

public record PlatformStats(
    int TotalContributions,
    int TotalContributors,
    int TotalCommunities,
    int TotalPointsAwarded,
    double AverageScore,
    int SpamBlocked,
    int DuplicatesBlocked,
    int ContributionsLast24h,
    string ScoringEngine);
