using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Replay of the weekly carry chain; this is where the ISO year boundary must hold.</summary>
public class LlmQuotaLedgerTests
{
    private const long Base = 50_000L;
    private const int Percent = 25;

    private static LlmQuotaLedger.WeekKey Week(int year, int week) => new(year, week);

    private static Dictionary<LlmQuotaLedger.WeekKey, long> Consumed(params (int Year, int Week, long Tokens)[] rows)
        => rows.ToDictionary(r => Week(r.Year, r.Week), r => r.Tokens);

    private static LlmQuotaLedger.BackfillPlan Run(
        LlmQuotaLedger.WeekKey? latestClosed,
        long latestCarryOut,
        LlmQuotaLedger.WeekKey? firstCharged,
        IReadOnlyDictionary<LlmQuotaLedger.WeekKey, long> consumed,
        int currentYear,
        int currentWeek,
        long baseWeekly = Base,
        int percent = Percent)
        => LlmQuotaLedger.Backfill(latestClosed, latestCarryOut, firstCharged, consumed,
            baseWeekly, percent, currentYear, currentWeek);

    [Fact]
    public void Backfill_WithNoHistory_ProducesNothing()
    {
        var plan = Run(null, 0, null, Consumed(), 2026, 32);

        Assert.Equal(BackfillOutcome.Chain, plan.Outcome);
        Assert.Equal(0L, plan.CarryIn);
        Assert.Empty(plan.ToClose);
    }

    [Fact]
    public void Backfill_StartsAtTheFirstChargedWeek_NotAtTheDawnOfTime()
    {
        var plan = Run(null, 0, Week(2026, 30), Consumed((2026, 30, 10_000L)), 2026, 32);

        Assert.Equal([30, 31], plan.ToClose.Select(d => d.Week).ToArray());
        Assert.Equal(10_000L, plan.ToClose[0].Consumed);
        Assert.Equal(0L, plan.ToClose[0].CarryIn);
    }

    [Fact]
    public void Backfill_ClosesEachElapsedWeekOnce_AndChainsTheCarry()
    {
        var plan = Run(null, 0, Week(2026, 30), Consumed((2026, 30, 30_000L), (2026, 31, 50_000L)), 2026, 32);

        Assert.Equal(2, plan.ToClose.Count);
        // week 30: rest 20.000 → 25 % = 5.000
        Assert.Equal(5_000L, plan.ToClose[0].CarryOut);
        // week 31 inherits it, consumes 50.000 of 55.000 → rest 5.000 → 1.250
        Assert.Equal(5_000L, plan.ToClose[1].CarryIn);
        Assert.Equal(1_250L, plan.ToClose[1].CarryOut);
        Assert.Equal(1_250L, plan.CarryIn);
    }

    [Fact]
    public void Backfill_CrossesTheYearBoundary_ThroughWeek53()
    {
        var plan = Run(null, 0, Week(2026, 52), Consumed((2026, 52, 1_000L)), 2027, 2);

        Assert.Equal(
            [(2026, 52), (2026, 53), (2027, 1)],
            plan.ToClose.Select(d => (d.Year, d.Week)).ToArray());
    }

    [Fact]
    public void Backfill_CrossesA52WeekBoundary()
    {
        var plan = Run(null, 0, Week(2027, 52), Consumed((2027, 52, 1_000L)), 2028, 1);

        Assert.Equal([(2027, 52)], plan.ToClose.Select(d => (d.Year, d.Week)).ToArray());
    }

    [Fact]
    public void Backfill_ResumesFromTheLatestClosedWeek()
    {
        var plan = Run(Week(2026, 30), 5_000L, Week(2026, 20), Consumed((2026, 31, 0L)), 2026, 32);

        Assert.Equal([31], plan.ToClose.Select(d => d.Week).ToArray());
        Assert.Equal(5_000L, plan.ToClose[0].CarryIn);
    }

    [Fact]
    public void Backfill_TreatsAnAlreadyClosedRunningWeekAsAnAnomaly()
    {
        var current = Run(Week(2026, 32), 9_000L, Week(2026, 20), Consumed(), 2026, 32);
        var future = Run(Week(2026, 40), 9_000L, Week(2026, 20), Consumed(), 2026, 32);

        Assert.Equal(BackfillOutcome.ReadPredecessor, current.Outcome);
        Assert.Equal(BackfillOutcome.ReadPredecessor, future.Outcome);
        Assert.Empty(current.ToClose);
    }

    [Fact]
    public void Backfill_StoppedByTheGuard_HandsOverNothing()
    {
        // 2025 (52) + 2026 (53) + 2027 (52) = 157 elapsed weeks, well past the guard
        var plan = Run(null, 0, Week(2025, 1), Consumed((2025, 1, 1_000L)), 2028, 1);

        Assert.Equal(LlmQuotaLedger.MaxBackfillWeeks, plan.ToClose.Count);
        Assert.Equal(0L, plan.CarryIn);
    }

    [Fact]
    public void Backfill_NeverExceedsTheCeiling_OverAYearOfIdleWeeks()
    {
        var plan = Run(null, 0, Week(2026, 1), Consumed((2026, 1, 1L)), 2026, 53, percent: 50);

        var ceiling = LlmQuotaMath.Ceiling(Base, 50);
        Assert.All(plan.ToClose, d => Assert.True(Base + d.CarryIn <= ceiling));
        Assert.True(Base + plan.CarryIn <= ceiling);
    }

    [Fact]
    public void Backfill_CarriesNothingAfterAFullyConsumedWeek()
    {
        var plan = Run(null, 0, Week(2026, 30),
            Consumed((2026, 30, 10_000L), (2026, 31, 60_000L)), 2026, 32);

        Assert.Equal(0L, plan.ToClose[1].CarryOut);
        Assert.Equal(0L, plan.CarryIn);
    }

    [Fact]
    public void Backfill_ClampsAStoredCarryThatOutgrewTheCurrentRule()
    {
        // the week before the running one was frozen at 40.000 under an older, larger base
        var plan = Run(Week(2026, 31), 40_000L, Week(2026, 20), Consumed(), 2026, 32,
            baseWeekly: 20_000, percent: 25);

        Assert.Empty(plan.ToClose);
        Assert.Equal(5_000L, plan.CarryIn);
    }

    [Fact]
    public void Backfill_IsIdempotent_WhenReRunFromItsOwnOutput()
    {
        var consumed = Consumed((2026, 30, 30_000L), (2026, 31, 50_000L));
        var first = Run(null, 0, Week(2026, 30), consumed, 2026, 32);
        var last = first.ToClose[^1];

        var second = Run(Week(last.Year, last.Week), last.CarryOut, Week(2026, 30), consumed, 2026, 32);

        Assert.Empty(second.ToClose);
        Assert.Equal(first.CarryIn, second.CarryIn);
    }
}
