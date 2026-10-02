using System.Net;
using System.Text;
using Zonar.Api.Data;
using Zonar.Api.Domain;
using Zonar.Api.Services;
using Zonar.Api.Services.Privacy;
using Zonar.Api.Services.Scoring;
using Microsoft.Extensions.Options;
using Zonar.Api.Options;

namespace Zonar.Api.Telegram;

/// <summary>
/// Handles one incoming Telegram message: bot commands get a reply,
/// ordinary group messages are scored and rewarded silently.
/// </summary>
public class TelegramUpdateHandler
{
    private readonly ContributionService _contributions;
    private readonly IContributionStore _store;
    private readonly IdentityHasher _hasher;
    private readonly TelegramApiClient _api;
    private readonly TelegramOptions _options;
    private readonly ScoringEngineInfo _engine;
    private readonly TimeProvider _clock;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(ContributionService contributions, IContributionStore store, IdentityHasher hasher,
        TelegramApiClient api, IOptions<TelegramOptions> options, ScoringEngineInfo engine, TimeProvider clock,
        ILogger<TelegramUpdateHandler> logger)
    {
        _contributions = contributions;
        _store = store;
        _hasher = hasher;
        _api = api;
        _options = options.Value;
        _engine = engine;
        _clock = clock;
        _logger = logger;
    }

    public async Task HandleAsync(TgUpdate update, string botUsername, CancellationToken ct)
    {
        var msg = update.Message;
        if (msg?.Text is null || msg.From is null || msg.From.IsBot) return;

        var text = msg.Text.Trim();
        var isGroup = msg.Chat.Type is "group" or "supergroup";
        var communityId = isGroup ? $"telegram:{msg.Chat.Id}" : "telegram:private";

        if (text.StartsWith('/'))
        {
            var (command, args) = ParseCommand(text, botUsername);
            if (command is null) return; // command meant for another bot

            var reply = command switch
            {
                "start" or "help" => HelpText(),
                "score" => await ScoreCommandAsync(args, ct),
                "me" => await MeCommandAsync(msg.From, ct),
                "top" => await TopCommandAsync(communityId, msg.Chat.Title, ct),
                "stats" => await StatsCommandAsync(ct),
                _ => "Unknown command. Try /help"
            };

            await _api.SendMessageAsync(msg.Chat.Id, reply, msg.MessageId, ct);
            return;
        }

        if (isGroup && _options.RecordGroupMessages)
        {
            var result = await _contributions.RecordAsync(new ContributionInput(
                CommunityExternalId: communityId,
                CommunityName: msg.Chat.Title ?? $"Telegram group {msg.Chat.Id}",
                Source: ContributionSource.Telegram,
                IdentityKey: $"telegram:{msg.From.Id}",
                DisplayName: null, // pseudonym – we do not store Telegram names
                Text: text), ct);

            _logger.LogInformation("Recorded Telegram contribution: score {Score}, +{Points} pts",
                result.Result.Score, result.RewardPoints);
        }
        else if (!isGroup)
        {
            await _api.SendMessageAsync(msg.Chat.Id,
                "Add me to a group to start earning points, or try <code>/score your message</code> to test the scorer.",
                msg.MessageId, ct);
        }
    }

    internal static (string? Command, string Args) ParseCommand(string text, string botUsername)
    {
        var space = text.IndexOf(' ');
        var head = space < 0 ? text[1..] : text[1..space];
        var args = space < 0 ? "" : text[(space + 1)..].Trim();

        var at = head.IndexOf('@');
        if (at >= 0)
        {
            var target = head[(at + 1)..];
            if (!target.Equals(botUsername, StringComparison.OrdinalIgnoreCase)) return (null, args);
            head = head[..at];
        }

        return (head.ToLowerInvariant(), args);
    }

    private static string HelpText() => """
        <b>Zonar Bot</b> – rewards valuable community messages 🧠

        I score every message in this group from 0–100 for usefulness and award points.
        Spam, duplicates and hype earn nothing. Message text is never stored.

        /score &lt;text&gt; – test how a message would score
        /me – your points and stats
        /top – this group's leaderboard (7 days)
        /stats – platform statistics
        """;

    private async Task<string> ScoreCommandAsync(string args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args))
            return "Usage: <code>/score your message here</code>";

        var r = await _contributions.PreviewAsync(args, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"<b>Score: {r.Score}/100</b> – {r.Label}");
        foreach (var reason in r.Reasons.Take(5))
        {
            var sign = reason.Impact > 0 ? "➕" : reason.Impact < 0 ? "➖" : "🤖";
            sb.AppendLine($"{sign} {Esc(reason.Text)}{(reason.Impact != 0 ? $" ({reason.Impact:+#;-#})" : "")}");
        }
        sb.Append($"<i>Engine: {Esc(r.ScorerUsed)}</i>");
        return sb.ToString();
    }

    private async Task<string> MeCommandAsync(TgUser user, CancellationToken ct)
    {
        var contributor = await _store.FindContributorByHashAsync(_hasher.HashIdentity($"telegram:{user.Id}"), ct);
        if (contributor is null) return "You have no contributions yet – join the conversation in a group!";

        var p = await _store.GetContributorProfileAsync(contributor.Id, ct);
        if (p is null) return "No stats yet.";

        return $"""
            <b>{Esc(p.DisplayName)}</b>
            🏆 Points: <b>{p.TotalPoints}</b>
            💬 Messages scored: {p.Contributions}
            📈 Average score: {p.AverageScore}
            ⭐ High-quality: {p.HighQualityCount} · 🚫 Spam: {p.SpamCount}
            """;
    }

    private async Task<string> TopCommandAsync(string communityExternalId, string? title, CancellationToken ct)
    {
        var community = await _store.FindCommunityAsync(communityExternalId, ct);
        if (community is null) return "No contributions in this chat yet.";

        var since = _clock.GetUtcNow().UtcDateTime.AddDays(-7);
        var board = await _store.GetLeaderboardAsync(community.Id, since, 10, ct);
        if (board.Count == 0) return "No contributions in the last 7 days.";

        var sb = new StringBuilder($"<b>🏆 Top contributors – {Esc(title ?? community.Name)}</b>\n");
        foreach (var e in board)
            sb.AppendLine($"{e.Rank}. {Esc(e.DisplayName)} – {e.TotalPoints} pts (avg {e.AverageScore})");
        return sb.ToString();
    }

    private async Task<string> StatsCommandAsync(CancellationToken ct)
    {
        var s = await _store.GetStatsAsync(_clock.GetUtcNow().UtcDateTime.AddHours(-24), _engine.Description, ct);
        return $"""
            <b>📊 Zonar stats</b>
            Messages scored: {s.TotalContributions} ({s.ContributionsLast24h} in 24h)
            Contributors: {s.TotalContributors} · Communities: {s.TotalCommunities}
            Points awarded: {s.TotalPointsAwarded}
            Average score: {s.AverageScore}
            Spam blocked: {s.SpamBlocked} · Duplicates blocked: {s.DuplicatesBlocked}
            """;
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}
