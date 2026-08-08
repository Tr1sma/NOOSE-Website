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

/// <summary>How NOOSEI ran over a window: success, speed, tool use, and how much of the prompt the endpoint served
/// from its own cache.</summary>
/// <remarks>
/// Quota tokens only, never money. Operating quality is not a price, and leaving cost out means the rule that keeps
/// real amounts away from everyone but the AI owner has nothing to catch on this page in the first place.
/// </remarks>
/// <param name="ToolSample">Requests the tool ranking was read from. The names live inside a JSON column that no
/// index reaches, so the ranking is a sample of the newest rows while every other figure covers the whole window —
/// said out loud, because a truncated ranking otherwise reads as a complete one.</param>
public sealed record LlmOperationsReport(
    int Days,
    int TotalRequests,
    int Failed,
    long QuotaTokens,
    long PromptTokens,
    long CachedTokens,
    int ToolCalls,
    int ToolFailures,
    int ToolSample,
    IReadOnlyList<LlmFeatureStat> ByFeature,
    IReadOnlyList<LlmCountStat> Tools,
    IReadOnlyList<LlmCountStat> Rounds,
    IReadOnlyList<LlmCountStat> FinishReasons,
    IReadOnlyList<LlmCountStat> Withdrawals,
    IReadOnlyList<LlmCountStat> Failures)
{
    public static LlmOperationsReport Empty { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [], [], [], []);

    /// <summary>Share of prompt tokens the endpoint served from its own cache — the measure of prompt-prefix
    /// stability, and the one figure that moves when a round stops breaking the cached prefix.</summary>
    public double CacheHitShare => PromptTokens <= 0 ? 0 : Math.Clamp((double)CachedTokens / PromptTokens, 0, 1);

    public double SuccessShare => TotalRequests <= 0 ? 0 : (double)(TotalRequests - Failed) / TotalRequests;

    /// <summary>Share of tool calls that produced nothing: dead, timed out, or a repeat answered from the transcript.</summary>
    public double ToolFailureShare => ToolCalls <= 0 ? 0 : (double)ToolFailures / ToolCalls;
}

/// <param name="MedianToolMs">Median of the turn's own duration minus the time spent inside the endpoint — what the
/// record database took. The number that says whether a slow answer was the model or the tools.</param>
public sealed record LlmFeatureStat(
    LlmFeature Feature, int Total, int Failed, long QuotaTokens, int MedianMs, int P95Ms, int MedianToolMs)
{
    public double SuccessShare => Total <= 0 ? 0 : (double)(Total - Failed) / Total;
}

/// <summary>One labelled bar of a distribution, biggest first.</summary>
public sealed record LlmCountStat(string Label, int Count);

/// <summary>What one ISO week actually cost across every agent.</summary>
/// <param name="Running">The week still in progress. It is real but incomplete, so a forecast must leave it out —
/// a week that is two days old would otherwise pull the average down by whatever is left of it.</param>
public sealed record LlmWeekSpend(
    int Year, int Week, DateTime StartLocal, long QuotaTokens, decimal CostUsd, bool Running);
