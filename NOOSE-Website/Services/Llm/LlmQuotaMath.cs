using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Token arithmetic of the AI quota. Single source of truth for service, UI and tests — never recompute inline.</summary>
public static class LlmQuotaMath
{
    /// <summary>The exchange rate: 1.000 quota tokens are worth one cent of real API cost.</summary>
    public const int TokensPerCent = 1_000;

    public const int TokensPerUsd = 100_000;

    /// <summary>Price assumed when no model price is configured. Deliberately low: a floor must never overstate a
    /// real bill, but it must never be zero either.</summary>
    public const decimal FallbackUsdPerMillionTokens = 0.20m;

    /// <summary>Real API cost to quota tokens. A non-free call never costs zero, so a spam loop still meters.</summary>
    public static long FromCost(decimal costUsd)
        => costUsd <= 0m
            ? 0L
            : Math.Max(1L, (long)Math.Round(costUsd * TokensPerUsd, 0, MidpointRounding.AwayFromZero));

    /// <summary>Quota tokens of a finished call, with a token-based floor under the reported cost.</summary>
    /// <remarks>
    /// <c>usage.cost</c> is optional, and a route that omits it would otherwise be free in every sense: the week
    /// never fills, the per-turn cost ceiling never trips, and the cost-spike rule skips the row because it only
    /// judges rows above zero. Taking the maximum rather than a fallback closes the same gap for a route that
    /// reports a token amount of cost instead of none at all.
    /// </remarks>
    public static long FromCost(decimal reportedUsd, int promptTokens, int completionTokens, LlmModelPrice? price)
    {
        var input = Math.Max(0, promptTokens);
        var output = Math.Max(0, completionTokens);
        var estimated = price is { } p
            ? (input * p.InputPerMillionUsd + output * p.OutputPerMillionUsd) / 1_000_000m
            : (input + output) * FallbackUsdPerMillionTokens / 1_000_000m;
        return FromCost(Math.Max(reportedUsd, estimated));
    }

    public static decimal ToCost(long tokens) => tokens / (decimal)TokensPerUsd;

    public static decimal ToCents(long tokens) => tokens / (decimal)TokensPerCent;

    /// <summary>Hard ceiling on what a week may inherit: the rank's share of its OWN base.</summary>
    public static long CarryCap(long baseWeekly, int carryPercent)
        => baseWeekly <= 0 || carryPercent <= 0 ? 0L : baseWeekly * carryPercent / 100L;

    /// <summary>What a week hands to the next: its share of the unused rest, never above <see cref="CarryCap"/>.</summary>
    /// <remarks>
    /// Deliberate deviation from <see cref="FinancingMath.CarryOut"/>, which is uncapped and therefore compounds.
    /// Capping at the share of the base makes hoarding impossible: available settles at base + cap after a single
    /// unused week and stays there forever. Clamping the rest before the multiplication is the same value as
    /// min-of-two-floors, because flooring is monotone — and it cannot overflow.
    /// </remarks>
    public static long CarryOut(long baseWeekly, long carryIn, long consumed, int carryPercent)
    {
        var rest = baseWeekly + carryIn - consumed;
        return rest <= 0 || carryPercent <= 0 || baseWeekly <= 0
            ? 0L
            : Math.Min(rest, baseWeekly) * carryPercent / 100L;
    }

    /// <summary>Most an agent may ever hold in one week.</summary>
    public static long Ceiling(long baseWeekly, int carryPercent) => baseWeekly + CarryCap(baseWeekly, carryPercent);

    /// <summary>Ceiling for a single local day, as a share of the weekly base; 0 means no daily ceiling.</summary>
    /// <remarks>
    /// Deliberately measured against the base and not against <see cref="Ceiling" />: a week that inherited carry-over
    /// is meant to last longer, not to be spendable faster. The share is well above a seventh, because a real day of
    /// work is bursty — this stops a runaway loop from eating the week in an hour, it does not ration anyone.
    /// </remarks>
    public static long DailyLimit(long baseWeekly, int dailyPercent)
        => baseWeekly <= 0 || dailyPercent <= 0 ? 0L : Math.Max(1L, baseWeekly * dailyPercent / 100L);

    /// <summary>Cap a stored carry-over on read; a rule lowered after a week was frozen must not keep handing out the old amount.</summary>
    public static long ClampCarryIn(long storedCarryOut, long baseWeekly, int carryPercent)
        => Math.Clamp(storedCarryOut, 0L, CarryCap(baseWeekly, carryPercent));
}
