namespace Zonar.Api.Telegram;

// Minimal subset of the Telegram Bot API (https://core.telegram.org/bots/api).
// Property names are mapped with snake_case JSON (update_id, message_id, ...).

public record TgResponse<T>(bool Ok, T? Result, string? Description);

public record TgUpdate(long UpdateId, TgMessage? Message);

public record TgMessage(long MessageId, TgUser? From, TgChat Chat, string? Text);

public record TgUser(long Id, bool IsBot, string? FirstName, string? Username);

public record TgChat(long Id, string Type, string? Title);
