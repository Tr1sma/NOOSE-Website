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

    /// <summary>Preferred upstream providers in order (OpenRouter `provider.order`). Empty = let the router choose by <see cref="ProviderSort"/>. A popular model is served by many providers of very different capability, health and latency — pinning the ones that work keeps a routing roll of the dice from surfacing as a timeout.</summary>
    public List<string> Providers { get; set; } = new();

    /// <summary>Providers that must never serve this deployment (OpenRouter `provider.ignore`), e.g. because they reject the system+user request shape.</summary>
    public List<string> IgnoreProviders { get; set; } = new();

    /// <summary>Fall back to other providers when the preferred ones fail; only meaningful with <see cref="Providers"/>.</summary>
    public bool AllowProviderFallbacks { get; set; } = true;

    /// <summary>Routing preference when no explicit order is set: latency, throughput or price.</summary>
    public string ProviderSort { get; set; } = "latency";

    /// <summary>Budget per attempt; a stalled provider is abandoned instead of eating the whole request.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 25;

    /// <summary>Hard ceiling over all attempts (HttpClient timeout).</summary>
    public int TotalTimeoutSeconds { get; set; } = 90;

    /// <summary>Extra attempts after the first on transient upstream failures (429/404/5xx/timeout).</summary>
    public int Retries { get; set; } = 2;

    /// <summary>Delay before a retry.</summary>
    public int RetryDelayMs { get; set; } = 750;

    /// <summary>True only when enabled AND fully configured.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}
