using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Zonar.Api.Contracts;
using Zonar.Api.Data;
using Zonar.Api.Domain;
using Zonar.Api.Services;
using Zonar.Api.Services.Scoring;

namespace Zonar.Api.Endpoints;

public static partial class ApiEndpoints
{
    public const string ScoringRateLimit = "scoring";

    public static void MapZonarApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // ---------- Scoring ----------
        api.MapPost("/score", ScoreAsync)
            .WithTags("Scoring")
            .WithSummary("Score a message (nothing is saved)")
            .RequireRateLimiting(ScoringRateLimit);

        api.MapPost("/contributions", ContributeAsync)
            .WithTags("Scoring")
            .WithSummary("Score a message and award points to a nickname (website demo)")
            .RequireRateLimiting(ScoringRateLimit);

        // ---------- Read models ----------
        api.MapGet("/leaderboard", LeaderboardAsync)
            .WithTags("Community")
            .WithSummary("Top contributors by points");

        api.MapGet("/contributors/{id:int}", ContributorAsync)
            .WithTags("Community")
            .WithSummary("A contributor's profile and recent scores");

        api.MapGet("/communities", async (IContributionStore store, CancellationToken ct) =>
                TypedResults.Ok(await store.GetCommunitiesAsync(ct)))
            .WithTags("Community")
            .WithSummary("All communities (website, demo, Telegram groups)");

        api.MapGet("/stats", async (IContributionStore store, ScoringEngineInfo engine, TimeProvider clock, CancellationToken ct) =>
                TypedResults.Ok(await store.GetStatsAsync(clock.GetUtcNow().UtcDateTime.AddHours(-24), engine.Description, ct)))
            .WithTags("Community")
            .WithSummary("Platform-wide statistics");
    }

    private static async Task<Results<Ok<ScoreResponse>, ValidationProblem>> ScoreAsync(
        ScoreRequest request, ContributionService service, CancellationToken ct)
    {
        var errors = ValidateMessage(request.Message);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var result = await service.PreviewAsync(request.Message, ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<Results<Ok<ContributionResponse>, ValidationProblem>> ContributeAsync(
        ContributionRequest request, ContributionService service, CancellationToken ct)
    {
        var errors = ValidateMessage(request.Message);
        var nickname = request.Nickname?.Trim() ?? "";
        if (!NicknameRegex().IsMatch(nickname))
            errors["nickname"] = new[] { "Nickname must be 2–24 characters: letters, numbers, spaces, _ . -" };
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var response = await service.RecordAsync(new ContributionInput(
            CommunityExternalId: "web",
            CommunityName: "Website Demo",
            Source: ContributionSource.Web,
            IdentityKey: $"web:{nickname}",
            DisplayName: nickname,
            Text: request.Message), ct);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<IReadOnlyList<LeaderboardEntry>>, ValidationProblem>> LeaderboardAsync(
        IContributionStore store, TimeProvider clock, CancellationToken ct,
        int? communityId = null, int days = 7, int top = 10)
    {
        if (days is < 1 or > 365 || top is < 1 or > 100)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["query"] = new[] { "days must be 1–365 and top must be 1–100" }
            });

        var since = clock.GetUtcNow().UtcDateTime.AddDays(-days);
        return TypedResults.Ok(await store.GetLeaderboardAsync(communityId, since, top, ct));
    }

    private static async Task<Results<Ok<ContributorProfile>, NotFound>> ContributorAsync(
        int id, IContributionStore store, CancellationToken ct)
    {
        var profile = await store.GetContributorProfileAsync(id, ct);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }

    private static Dictionary<string, string[]> ValidateMessage(string? message)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(message))
            errors["message"] = new[] { "Message is required." };
        else if (message.Length > ContributionService.MaxMessageLength)
            errors["message"] = new[] { $"Message must be at most {ContributionService.MaxMessageLength} characters." };
        return errors;
    }

    [GeneratedRegex(@"^[\p{L}\p{N} _.\-]{2,24}$")]
    private static partial Regex NicknameRegex();
}
