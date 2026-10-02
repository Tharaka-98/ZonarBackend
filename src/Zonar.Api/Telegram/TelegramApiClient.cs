using System.Net.Http.Json;
using System.Text.Json;

namespace Zonar.Api.Telegram;

/// <summary>
/// Thin wrapper over the Telegram Bot HTTP API using plain HttpClient – no SDK needed.
/// The HttpClient's BaseAddress is https://api.telegram.org/bot{token}/ (configured in Program.cs).
/// </summary>
public class TelegramApiClient
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public TelegramApiClient(HttpClient http) => _http = http;

    public async Task<TgUser> GetMeAsync(CancellationToken ct)
    {
        var res = await _http.GetFromJsonAsync<TgResponse<TgUser>>("getMe", Json, ct);
        return EnsureOk(res);
    }

    public async Task<IReadOnlyList<TgUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct)
    {
        var url = $"getUpdates?offset={offset}&timeout={timeoutSeconds}&allowed_updates=%5B%22message%22%5D";
        var res = await _http.GetFromJsonAsync<TgResponse<List<TgUpdate>>>(url, Json, ct);
        return EnsureOk(res);
    }

    /// <summary>Registers the command menu shown when users type "/".</summary>
    public Task SetMyCommandsAsync(IEnumerable<(string Command, string Description)> commands, CancellationToken ct)
        => PostAsync("setMyCommands", new
        {
            commands = commands.Select(c => new { command = c.Command, description = c.Description }).ToArray()
        }, ct);

    /// <summary>Text shown in an empty chat with the bot ("What can this bot do?").</summary>
    public Task SetMyDescriptionAsync(string description, CancellationToken ct)
        => PostAsync("setMyDescription", new { description }, ct);

    /// <summary>Short text shown on the bot's profile page.</summary>
    public Task SetMyShortDescriptionAsync(string shortDescription, CancellationToken ct)
        => PostAsync("setMyShortDescription", new { short_description = shortDescription }, ct);

    private async Task PostAsync(string method, object body, CancellationToken ct)
    {
        using var content = new StringContent(JsonSerializer.Serialize(body, Json), System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(method, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendMessageAsync(long chatId, string html, long? replyToMessageId, CancellationToken ct)
    {
        var body = new
        {
            chat_id = chatId,
            text = html,
            parse_mode = "HTML",
            reply_parameters = replyToMessageId is null
                ? null
                : new { message_id = replyToMessageId, allow_sending_without_reply = true }
        };

        // StringContent (not PostAsJsonAsync) so a Content-Length header is sent instead of chunked encoding.
        using var content = new StringContent(JsonSerializer.Serialize(body, Json), System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("sendMessage", content, ct);
        response.EnsureSuccessStatusCode();
    }

    private static T EnsureOk<T>(TgResponse<T>? res)
    {
        if (res is null || !res.Ok || res.Result is null)
            throw new InvalidOperationException($"Telegram API error: {res?.Description ?? "no response"}");
        return res.Result;
    }
}
