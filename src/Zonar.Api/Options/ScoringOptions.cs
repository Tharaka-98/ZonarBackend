namespace Zonar.Api.Options;

public class ScoringOptions
{
    public const string Section = "Scoring";

    /// <summary>"Rules" (default, offline) or "OpenAI" (any OpenAI-compatible chat API).</summary>
    public string Provider { get; set; } = "Rules";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>How much the LLM score counts in the final blend (0..1). The rest comes from the rule engine.</summary>
    public double LlmWeight { get; set; } = 0.6;

    public bool UseLlm =>
        string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
