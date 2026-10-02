namespace Zonar.Api.Options;

public class TelegramOptions
{
    public const string Section = "Telegram";

    public bool Enabled { get; set; }
    public string? BotToken { get; set; }

    /// <summary>Score and reward ordinary group messages (needs privacy mode OFF in BotFather).</summary>
    public bool RecordGroupMessages { get; set; } = true;

    /// <summary>Bot API host (override only for testing).</summary>
    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

    public int PollTimeoutSeconds { get; set; } = 25;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BotToken);
}
