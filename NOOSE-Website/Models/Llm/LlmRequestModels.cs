using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Llm;

/// <summary>Filter of the NOOSEI request log.</summary>
public sealed class LlmRequestFilter
{
    public string? AgentId { get; set; }

    public LlmFeature? Feature { get; set; }

    public string? Model { get; set; }

    /// <summary>null = both, true = only successes, false = only failures.</summary>
    public bool? Success { get; set; }

    public bool AnomalousOnly { get; set; }

    public string? Text { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    /// <summary>Hard cap on returned rows; the viewer pages the result client-side.</summary>
    public const int MaxRows = 1000;
}

/// <summary>One row of the request log.</summary>
public sealed record LlmRequestRow(
    string Id,
    DateTime TimestampUtc,
    string? AgentName,
    LlmFeature Feature,
    string? Model,
    string? Provider,
    int PromptTokens,
    int CompletionTokens,
    int CachedTokens,
    long QuotaTokens,
    decimal CostUsd,
    int DurationMs,
    int ToolRounds,
    bool Success,
    string? ErrorMessage,
    bool IsAnomalous,
    LlmAnomalyKind? AnomalyKind);

/// <summary>Full content of one request, for the detail dialog.</summary>
public sealed record LlmRequestDetail(
    LlmRequestRow Row,
    string? Prompt,
    string? Answer,
    IReadOnlyList<LlmContextRef> ContextRefs);

public sealed record LlmRequestPage(IReadOnlyList<LlmRequestRow> Rows, int TotalCount, long TotalQuotaTokens, decimal TotalCostUsd)
{
    public bool Capped => TotalCount > Rows.Count;
}

/// <summary>Selectable values of the log filter.</summary>
public sealed record LlmRequestFilterOptions(IReadOnlyList<LlmRequestAgentOption> Agents, IReadOnlyList<string> Models);

public sealed record LlmRequestAgentOption(string Id, string Codename);

/// <summary>One point of an agent's weekly consumption trend.</summary>
public sealed record LlmWeekPoint(int Year, int Week, DateTime StartLocal, long Consumed, long Available);
