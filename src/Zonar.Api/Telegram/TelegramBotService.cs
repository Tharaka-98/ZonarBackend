using Microsoft.Extensions.Options;
using Zonar.Api.Options;

namespace Zonar.Api.Telegram;

/// <summary>
/// Background worker that long-polls Telegram for new messages (no public URL / webhook needed,
/// so it also works on a laptop during the demo). Each update is handled in its own DI scope.
/// </summary>
public class TelegramBotService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TelegramApiClient _api;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(IServiceScopeFactory scopes, TelegramApiClient api,
        IOptions<TelegramOptions> options, ILogger<TelegramBotService> logger)
    {
        _scopes = scopes;
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sets the bot's "/" command menu and descriptions via the Bot API, so nothing has to be
    /// configured by hand in BotFather (except privacy mode and the profile picture).
    /// </summary>
    private async Task ConfigureBotProfileAsync(CancellationToken ct)
    {
        try
        {
            await _api.SetMyCommandsAsync(new[]
            {
                ("score", "Test how a message would score"),
                ("me", "Your points and stats"),
                ("top", "This group's leaderboard (7 days)"),
                ("stats", "Platform statistics"),
                ("help", "How Zonar works")
            }, ct);

            await _api.SetMyDescriptionAsync(
                "Zonar scores community messages for quality (0-100) and rewards valuable contributors. " +
                "Spam, hype and duplicates earn nothing. Message text is never stored.\n\n" +
                "Add me to a group, or try /score <your message>.", ct);

            await _api.SetMyShortDescriptionAsync(
                "Rewards valuable community messages. Spam earns nothing. Privacy first.", ct);

            _logger.LogInformation("Telegram bot commands and description configured");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not set bot commands/description (bot still works)");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
            return; // bot disabled – nothing to do

        string botUsername;
        while (true)
        {
            try
            {
                var me = await _api.GetMeAsync(stoppingToken);
                botUsername = me.Username ?? "";
                _logger.LogInformation("Telegram bot @{Bot} started (long polling)", botUsername);
                await ConfigureBotProfileAsync(stoppingToken);
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError("Telegram rejected the bot token – check Telegram:BotToken (or create a new one with /revoke in BotFather)");
                return; // retrying will not help
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cannot reach api.telegram.org ({Error}). Check your internet/VPN/firewall. Retrying in 30s...",
                    ex.GetBaseException().Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        long offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _api.GetUpdatesAsync(offset, _options.PollTimeoutSeconds, stoppingToken);
                foreach (var update in updates)
                {
                    offset = update.UpdateId + 1; // acknowledge even if handling fails, so one bad message can't block the bot
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>();
                        await handler.HandleAsync(update, botUsername, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Failed to handle Telegram update {UpdateId}", update.UpdateId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram polling error – retrying in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
