namespace NOOSE_Website.Models.Llm;

/// <summary>LLM endpoint configuration. Secrets (ApiKey) come from user-secrets / env, never the repo.</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Master switch; when false the assistant stays fully inert.</summary>
    public bool Enabled { get; set; }

    /// <summary>OpenAI-compatible base URL (e.g. OpenRouter).</summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    /// <summary>Allow classified/VS record content to be sent to the endpoint. Default false.</summary>
    public bool AllowClassifiedContent { get; set; }

    /// <summary>True only when enabled AND fully configured.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}
