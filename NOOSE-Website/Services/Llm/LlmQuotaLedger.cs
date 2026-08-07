namespace NOOSE_Website.Services;

/// <summary>What the caller must do after replaying the carry chain.</summary>
public enum BackfillOutcome
{
    /// <summary>The chain was replayed; close the drafts and use the returned carry-over.</summary>
    Chain = 0,

    /// <summary>The running week (or a later one) is already closed — an anomaly. Read the direct predecessor's stored carry instead.</summary>
    ReadPredecessor = 1,
}

/// <summary>Replay of the weekly quota carry chain. Pure: no database, no clock, so every rollover is testable.</summary>
public static class LlmQuotaLedger
{
    /// <summary>Two years of weeks; a chain older than this cannot reach the running week anyway.</summary>
    public const int MaxBackfillWeeks = 104;

    public readonly record struct WeekKey(int Year, int Week);

    /// <summary>One elapsed week ready to be frozen into the period ledger.</summary>
    public sealed record PeriodDraft(
        int Year,
        int Week,
        long BaseWeekly,
        long CarryIn,
        long Consumed,
        long CarryOut,
        int CarryPercent);

    public sealed record BackfillPlan(BackfillOutcome Outcome, long CarryIn, IReadOnlyList<PeriodDraft> ToClose);

    private static readonly BackfillPlan Predecessor = new(BackfillOutcome.ReadPredecessor, 0L, []);

    /// <summary>Replays the chain up to (but not into) the running week and returns what that week may inherit.</summary>
    public static BackfillPlan Backfill(
        WeekKey? latestClosed,
        long latestCarryOut,
        WeekKey? firstCharged,
        IReadOnlyDictionary<WeekKey, long> consumedByWeek,
        long baseWeekly,
        int carryPercent,
        int currentYear,
        int currentWeek)
    {
        int year, week;
        long carry;

        if (latestClosed is { } latest)
        {
            if (!IsoWeekPeriod.IsBefore(latest.Year, latest.Week, currentYear, currentWeek))
            {
                return Predecessor;
            }
            (year, week) = IsoWeekPeriod.Next(latest.Year, latest.Week);
            carry = latestCarryOut;
        }
        else if (firstCharged is { } first)
        {
            // start at the earliest week that actually carries a charge: weeks before that had nothing
            // to spend, so they must not manufacture carry-over out of an untouched quota
            (year, week) = (first.Year, first.Week);
            carry = 0L;
        }
        else
        {
            return new BackfillPlan(BackfillOutcome.Chain, 0L, []);
        }

        var drafts = new List<PeriodDraft>();
        var guard = 0;
        while (IsoWeekPeriod.IsBefore(year, week, currentYear, currentWeek) && guard++ < MaxBackfillWeeks)
        {
            var consumed = consumedByWeek.TryGetValue(new WeekKey(year, week), out var used) ? used : 0L;
            var carryOut = LlmQuotaMath.CarryOut(baseWeekly, carry, consumed, carryPercent);
            drafts.Add(new PeriodDraft(year, week, baseWeekly, carry, consumed, carryOut, carryPercent));
            carry = carryOut;
            (year, week) = IsoWeekPeriod.Next(year, week);
        }

        // carry may only come from the DIRECT predecessor; a backfill cut short by the guard hands over nothing
        var carryIn = year == currentYear && week == currentWeek
            ? LlmQuotaMath.ClampCarryIn(carry, baseWeekly, carryPercent)
            : 0L;
        return new BackfillPlan(BackfillOutcome.Chain, carryIn, drafts);
    }
}
