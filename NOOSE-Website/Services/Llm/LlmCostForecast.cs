using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>What a week can cost at worst and what it is likely to cost. Pure, so both are testable without a clock.</summary>
public static class LlmCostForecast
{
    /// <summary>Every agent's full weekly allowance added up — the bill if all of them spent to the last token.</summary>
    /// <remarks>Built from <c>Available</c> (base plus carry-in), not from the rank base: an override or a carried
    /// balance is part of what may actually be spent this week, and the ceiling has to include it.</remarks>
    public static long MaxTokens(IEnumerable<LlmQuotaStatus> statuses)
        => statuses.Sum(s => Math.Max(0, s.Available));

    /// <summary>Mean spend of the closed weeks, or null while none has closed.</summary>
    /// <remarks>The running week is excluded rather than counted: it is real but unfinished, and including it
    /// would make the forecast dip every Monday and climb back through the week.</remarks>
    public static (long Tokens, decimal CostUsd, int Weeks)? Expected(IReadOnlyList<LlmWeekSpend> weeks)
    {
        var closed = weeks.Where(w => !w.Running).ToList();
        if (closed.Count == 0)
        {
            return null;
        }
        var tokens = (long)Math.Round(closed.Average(w => (double)w.QuotaTokens), MidpointRounding.AwayFromZero);
        var cost = closed.Sum(w => w.CostUsd) / closed.Count;
        return (tokens, cost, closed.Count);
    }
}
