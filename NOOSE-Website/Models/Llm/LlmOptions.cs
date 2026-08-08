using NOOSE_Website.Models.Enums;

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

    /// <summary>Per-feature model override; anything unset falls back to <see cref="Model"/>. Proofreading a few
    /// paragraphs and a six-round analysis are very different jobs and do not need the same model.</summary>
    public Dictionary<LlmFeature, string> ModelByFeature { get; set; } = new();

    /// <summary>The model a feature runs on: its override when configured, otherwise the default.</summary>
    public string ModelFor(LlmFeature feature)
        => ModelByFeature.TryGetValue(feature, out var model) && !string.IsNullOrWhiteSpace(model)
            ? model.Trim()
            : Model;

    /// <summary>List price per model, for the token-based cost floor in <see cref="LlmQuotaMath" />. Deploy
    /// configuration like the model and the key — a price is money and never belongs in the database.</summary>
    public Dictionary<string, LlmModelPrice> PriceByModel { get; set; } = new();

    /// <summary>Configured price of a model, matched case-insensitively; null when none is set.</summary>
    public LlmModelPrice? PriceFor(string? model)
    {
        if(string.IsNullOrWhiteSpace(model))
        {
            return null;
        }
        foreach (var (key, price) in PriceByModel)
        {
            if (string.Equals(key, model, StringComparison.OrdinalIgnoreCase))
            {
                return price;
            }
        }
        return null;
    }

    /// <summary>Deployment-wide egress kill switch for classified/VS content. The per-record decision is made by the
    /// viewer's own scope long before this point; flip it off only when pointing at an endpoint you do not trust.</summary>
    public bool AllowClassifiedEgress { get; set; } = true;

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

    /// <summary>Delay before the first retry; doubles per attempt. Zero disables waiting entirely.</summary>
    public int RetryDelayMs { get; set; } = 750;

    /// <summary>Ceiling on the backoff, and on an endpoint's own Retry-After hint.</summary>
    public int RetryMaxDelayMs { get; set; } = 6_000;

    /// <summary>Offer the record-database tools to the model at all; off makes NOOSEI answer without file access.</summary>
    public bool ToolsEnabled { get; set; } = true;

    /// <summary>Tool rounds one turn may spend before the tools are withdrawn and an answer is forced.</summary>
    public int MaxToolRounds { get; set; } = 6;

    /// <summary>Tool calls executed per round; extras get a German refusal the model can recover from.</summary>
    public int MaxToolCallsPerRound { get; set; } = 4;

    /// <summary>Budget of a single tool invocation.</summary>
    public int ToolTimeoutSeconds { get; set; } = 15;

    /// <summary>Ceiling over the whole turn; the HttpClient timeout only bounds one round.</summary>
    public int TurnTimeoutSeconds { get; set; } = 120;

    /// <summary>Token budget of the replayed conversation history. Measured in tokens, not rows: one turn with four
    /// tool calls fills six rows, so a row count is both too short for a conversation and too expensive per round.</summary>
    public int HistoryTokenBudget { get; set; } = 12_000;

    /// <summary>Ceiling on how many past turns are replayed, whatever the budget allows.</summary>
    public int HistoryTurns { get; set; } = 8;

    /// <summary>Ceiling on the answer of a feature, in tokens; anything unset leaves it to the endpoint's default.
    /// Without one <see cref="LlmResult.FinishReason" /> never says "length" and a cut-off answer is stored as if
    /// it were whole — the model then reads its own torso back as a finished statement.</summary>
    public Dictionary<LlmFeature, int> MaxAnswerTokensByFeature { get; set; } = new()
    {
        [LlmFeature.Chat] = 1_200,
    };

    /// <summary>Answer ceiling of a feature, or null when it has none.</summary>
    public int? MaxAnswerTokensFor(LlmFeature feature)
        => MaxAnswerTokensByFeature.TryGetValue(feature, out var max) && max > 0 ? max : null;

    /// <summary>Beyond this the tools are withdrawn for the rest of the turn.</summary>
    public decimal MaxCostPerTurnUsd { get; set; } = 0.05m;

    /// <summary>Second budget guard, for a provider that reports no cost.</summary>
    public int MaxTokensPerTurn { get; set; } = 120_000;

    /// <summary>Where the structured-output ladder starts.</summary>
    public StructuredOutputMode StructuredOutput { get; set; } = StructuredOutputMode.Strict;

    /// <summary>Send `provider.require_parameters` so only providers that actually support schema/tools are routed to.</summary>
    public bool RequireCapableProviders { get; set; } = true;

    /// <summary>Append the upstream error detail to the message an agent sees. Off by default: it can name the model.</summary>
    public bool ExposeUpstreamDetail { get; set; }

    /// <summary>True only when enabled AND fully configured.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}

/// <summary>Per-million-token list price of one model.</summary>
public sealed class LlmModelPrice
{
    public decimal InputPerMillionUsd { get; set; }

    public decimal OutputPerMillionUsd { get; set; }
}

/// <summary>Where the structured-output fallback ladder starts for a deployment.</summary>
public enum StructuredOutputMode
{
    /// <summary>Enforced JSON schema first, then widen, then JSON mode.</summary>
    Strict = 0,

    /// <summary>Skip the capable-provider filter; schema first, then JSON mode.</summary>
    Lenient = 1,

    /// <summary>Straight to JSON mode with the schema in the prompt; for endpoints known not to support schemas.</summary>
    PromptOnly = 2,
}
