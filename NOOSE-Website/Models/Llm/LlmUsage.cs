namespace NOOSE_Website.Models.Llm;

/// <summary>Metered cost of one round, as the endpoint reports it. Consumed by the quota subsystem.</summary>
public readonly record struct LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int CachedPromptTokens,
    int ReasoningTokens,
    decimal CostUsd)
{
    public static LlmUsage Empty => default;

    /// <summary>Accumulates the rounds of one turn; the agent loop bills the sum, not each round.</summary>
    public static LlmUsage operator +(LlmUsage a, LlmUsage b) => new(
        a.PromptTokens + b.PromptTokens,
        a.CompletionTokens + b.CompletionTokens,
        a.TotalTokens + b.TotalTokens,
        a.CachedPromptTokens + b.CachedPromptTokens,
        a.ReasoningTokens + b.ReasoningTokens,
        a.CostUsd + b.CostUsd);
}
