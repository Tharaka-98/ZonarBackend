using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Zonar.Api.Contracts;

namespace Zonar.Api.Tests;

/// <summary>Spins up the real API in memory with a throw-away SQLite database.</summary>
public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.ZonarFactory>
{
    public class ZonarFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"zonar-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // Added last, so these override appsettings*.json, appsettings.Local.json and user secrets.
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Zonar"] = $"Data Source={_dbPath}",
                ["Seed:DemoData"] = "false",
                ["RateLimiting:ScoringPerMinute"] = "1000",
                ["Telegram:Enabled"] = "false",   // never start the real bot during tests
                ["Scoring:Provider"] = "Rules"
            }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public ApiIntegrationTests(ZonarFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_is_healthy()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Score_returns_result_with_reasons()
    {
        var res = await _client.PostAsJsonAsync("/api/score", new ScoreRequest("Why did volume drop 20% this week? I think liquidity moved because of the audit news."));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ScoreResponse>(Json);
        Assert.NotNull(body);
        Assert.InRange(body!.Score, 0, 100);
        Assert.NotEmpty(body.Reasons);
    }

    [Fact]
    public async Task Empty_message_is_rejected()
    {
        var res = await _client.PostAsJsonAsync("/api/score", new ScoreRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_nickname_is_rejected()
    {
        var res = await _client.PostAsJsonAsync("/api/contributions", new ContributionRequest("<script>", "Hello there everyone"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Contribution_earns_points_once_and_appears_on_leaderboard()
    {
        var request = new ContributionRequest("TestUser", "Compared with last month, volume is up 40% because more traders joined. What does the sentiment data show?");

        var first = await (await _client.PostAsJsonAsync("/api/contributions", request)).Content.ReadFromJsonAsync<ContributionResponse>(Json);
        var second = await (await _client.PostAsJsonAsync("/api/contributions", request)).Content.ReadFromJsonAsync<ContributionResponse>(Json);

        Assert.True(first!.RewardPoints > 0);
        Assert.True(second!.IsDuplicate);
        Assert.Equal(0, second.RewardPoints);

        var board = await _client.GetFromJsonAsync<List<LeaderboardEntry>>("/api/leaderboard", Json);
        Assert.Contains(board!, e => e.DisplayName == "TestUser" && e.TotalPoints == first.RewardPoints);

        var profile = await _client.GetFromJsonAsync<ContributorProfile>($"/api/contributors/{first.ContributorId}", Json);
        Assert.Equal(2, profile!.Contributions);
    }

    [Fact]
    public async Task Unknown_contributor_returns_404()
    {
        var res = await _client.GetAsync("/api/contributors/999999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
