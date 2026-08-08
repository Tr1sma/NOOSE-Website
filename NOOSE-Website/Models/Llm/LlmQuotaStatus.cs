using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Models.Llm;

/// <summary>One agent's NOOSEI quota for the running ISO week.</summary>
/// <param name="DailyLimit">Ceiling for the running local day; 0 means the rank has none.</param>
/// <param name="ConsumedToday">Charged since local midnight. Counted apart from the week because the two ceilings
/// answer different questions: one is a fair share, the other is a runaway brake.</param>
public record LlmQuotaStatus(
    string AgentId,
    string? Codename,
    Rank? Rank,
    int Year,
    int Week,
    long BaseWeekly,
    long CarryIn,
    long Consumed,
    int CarryPercent,
    bool IsOverride,
    long DailyLimit = 0,
    long ConsumedToday = 0)
{
    /// <summary>What this week started with.</summary>
    public long Available => BaseWeekly + CarryIn;

    /// <summary>May go negative: the last permitted call is charged after it answered.</summary>
    public long Remaining => Available - Consumed;

    public bool IsBlocked => Remaining <= 0;

    /// <summary>Left for today, bounded by whatever is left of the week; long.MaxValue when there is no daily limit.</summary>
    public long RemainingToday => DailyLimit <= 0
        ? long.MaxValue
        : Math.Min(Remaining, DailyLimit - ConsumedToday);

    /// <summary>Today is spent but the week is not — a state that clears at local midnight, not on Monday.</summary>
    public bool IsDayBlocked => DailyLimit > 0 && !IsBlocked && ConsumedToday >= DailyLimit;

    public long CarryCap => LlmQuotaMath.CarryCap(BaseWeekly, CarryPercent);

    /// <summary>Most this agent can ever hold in one week.</summary>
    public long Ceiling => LlmQuotaMath.Ceiling(BaseWeekly, CarryPercent);

    public double UsedShare => Available <= 0 ? 1 : Math.Clamp((double)Consumed / Available, 0, 1);

    /// <summary>Local Monday 00:00 the quota refills at.</summary>
    public DateTime? NextResetLocal => Year <= 0 ? null : IsoWeekPeriod.Reset(Year, Week);

    public string PeriodLabel => Year <= 0 ? string.Empty : IsoWeekPeriod.Label(Year, Week);

    public static LlmQuotaStatus Empty { get; } = new(string.Empty, null, null, 0, 0, 0, 0, 0, 0, false);
}

/// <summary>What one booked request cost and what is left afterwards.</summary>
public sealed record LlmQuotaCharge(
    long QuotaTokens,
    decimal CostUsd,
    LlmQuotaStatus Status,
    LlmAnomalyKind? Anomaly,
    bool Persisted);

/// <summary>Everything the quota service needs to book a finished call.</summary>
public sealed record LlmChargeInput(
    string AgentId,
    LlmFeature Feature,
    LlmUsage Usage,
    string? Model,
    string? Provider,
    int DurationMs,
    int ToolRounds,
    bool Success,
    string? ErrorMessage,
    string? Prompt,
    string? Answer,
    IReadOnlyList<LlmContextRef>? ContextRefs,
    LlmRequestTrace? Trace = null);

/// <summary>How a turn ran, beyond what it cost. Every field is optional, and an unset one means "not measured" —
/// a caller that knows nothing about tools must not book a turn as one that made none.</summary>
/// <param name="ModelLatencyMs">Time inside the endpoint, summed over the rounds. The difference to the turn's own
/// duration is the tool budget, which is the number that says whether a timeout was the model or the database.</param>
/// <param name="ToolFailures">Calls that produced nothing — dead, timed out, or a repeat answered from the
/// transcript. Counted apart from <paramref name="ToolCalls" /> so a rising share is visible before it is a bug
/// report.</param>
public sealed record LlmRequestTrace(
    string? FinishReason = null,
    int? Attempts = null,
    int? ModelLatencyMs = null,
    int? ToolCalls = null,
    int? ToolFailures = null,
    bool? Degraded = null,
    LlmToolWithdrawal? Withdrawal = null,
    LlmFailureKind? FailureKind = null)
{
    /// <summary>Which bucket an exception belongs in. The agent's own cancellation is told apart from the turn
    /// budget by asking whose token fired, not by the exception type — both arrive as the same type.</summary>
    public static LlmFailureKind Classify(Exception exception, bool agentCancelled) => exception switch
    {
        LlmQuotaExceededException => LlmFailureKind.Quota,
        UnauthorizedAccessException => LlmFailureKind.Denied,
        LlmCapabilityException => LlmFailureKind.Capability,
        OperationCanceledException => agentCancelled ? LlmFailureKind.Cancelled : LlmFailureKind.Timeout,
        HttpRequestException => LlmFailureKind.Upstream,
        _ => LlmFailureKind.Unknown,
    };
}

/// <summary>A record or tool NOOSEI touched; the reference is logged, never the injected text.</summary>
public sealed record LlmContextRef(string Kind, string? Id, string? Name);
