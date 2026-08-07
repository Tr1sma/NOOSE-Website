using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Models.Llm;

/// <summary>One agent's NOOSEI quota for the running ISO week.</summary>
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
    bool IsOverride)
{
    /// <summary>What this week started with.</summary>
    public long Available => BaseWeekly + CarryIn;

    /// <summary>May go negative: the last permitted call is charged after it answered.</summary>
    public long Remaining => Available - Consumed;

    public bool IsBlocked => Remaining <= 0;

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
    IReadOnlyList<LlmContextRef>? ContextRefs);

/// <summary>A record or tool NOOSEI touched; the reference is logged, never the injected text.</summary>
public sealed record LlmContextRef(string Kind, string? Id, string? Name);
