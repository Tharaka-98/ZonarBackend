using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Zonar.Api.Data;
using Zonar.Api.Endpoints;
using Zonar.Api.Infrastructure;
using Zonar.Api.Options;
using Zonar.Api.Services;
using Zonar.Api.Services.Privacy;
using Zonar.Api.Services.Rewards;
using Zonar.Api.Services.Scoring;
using Zonar.Api.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Local secrets (bot token, API keys) live in appsettings.Local.json, which is git-ignored.
// Environment variables and command-line args still override it.
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var config = builder.Configuration;
var services = builder.Services;

// ---------- Configuration ----------
services.Configure<ScoringOptions>(config.GetSection(ScoringOptions.Section));
services.Configure<RewardOptions>(config.GetSection(RewardOptions.Section));
services.Configure<PrivacyOptions>(config.GetSection(PrivacyOptions.Section));
services.Configure<TelegramOptions>(config.GetSection(TelegramOptions.Section));

// ---------- Core services ----------
services.AddSingleton(TimeProvider.System);
services.AddSingleton<IdentityHasher>();
services.AddSingleton<RewardPolicy>();
services.AddSingleton<RuleBasedScorer>();
services.AddHttpClient(LlmScorer.HttpClientName);

services.AddSingleton<LlmScorer>();

// Options are read lazily (when first resolved) so test hosts and env vars can override them.
services.AddSingleton<IMessageScorer>(sp =>
    sp.GetRequiredService<IOptions<ScoringOptions>>().Value.UseLlm
        ? sp.GetRequiredService<LlmScorer>()
        : sp.GetRequiredService<RuleBasedScorer>());
services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<ScoringOptions>>().Value;
    return new ScoringEngineInfo(o.UseLlm ? $"Hybrid AI ({o.Model}) + rule engine" : "Explainable rule engine");
});

services.AddScoped<ContributionService>();
services.AddZonarData(config);

// ---------- Telegram bot (optional) ----------
services.AddHttpClient<TelegramApiClient>((sp, c) =>
{
    var tg = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    c.BaseAddress = new Uri($"{tg.ApiBaseUrl.TrimEnd('/')}/bot{tg.BotToken ?? "disabled"}/");
    c.Timeout = TimeSpan.FromSeconds(tg.PollTimeoutSeconds + 15);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    ConnectCallback = NetworkHelpers.ConnectPreferIPv4Async,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
});
services.AddScoped<TelegramUpdateHandler>();
services.AddHostedService<TelegramBotService>(); // exits immediately unless Telegram:Enabled + BotToken are set

// ---------- Web API plumbing ----------
services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
services.AddProblemDetails();
services.AddHealthChecks();
services.AddZonarSwagger();

var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST")));

services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ApiEndpoints.ScoringRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = ctx.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimiting:ScoringPerMinute", 20),
            Window = TimeSpan.FromMinutes(1)
        }));
});

services.Configure<ForwardedHeadersOptions>(o =>
{
    // Azure / Render / Docker run behind a reverse proxy – trust X-Forwarded-For so rate limiting sees the real client IP.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---------- Pipeline ----------
var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseZonarSwagger();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapZonarApi();

await app.InitialiseDatabaseAsync();

app.Logger.LogInformation("Scoring engine: {Engine} | Telegram bot: {Telegram}",
    app.Services.GetRequiredService<ScoringEngineInfo>().Description,
    app.Services.GetRequiredService<IOptions<TelegramOptions>>().Value.IsConfigured ? "enabled" : "disabled");

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }
