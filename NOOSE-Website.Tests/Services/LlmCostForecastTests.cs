using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The two numbers that say what NOOSEI costs: the ceiling and the trend.</summary>
public class LlmCostForecastTests
{
    private static LlmQuotaStatus Status(long baseWeekly, long carryIn = 0, long consumed = 0)
        => new("a", "Falke", Rank.SpecialAgent, 2026, 32, baseWeekly, carryIn, consumed, 50, false);

    private static LlmWeekSpend Week(int week, long tokens, decimal cost, bool running = false)
        => new(2026, week, new DateTime(2026, 1, 1).AddDays(7 * week), tokens, cost, running);

    // ---- ceiling ----

    [Fact]
    public void MaxTokens_AddsUpWhatEveryAgentCouldSpend()
    {
        var total = LlmCostForecast.MaxTokens([Status(20_000), Status(35_000, carryIn: 5_000), Status(50_000)]);

        // carry-in is spendable this week, so it belongs in the ceiling
        Assert.Equal(110_000, total);
    }

    [Fact]
    public void MaxTokens_IgnoresWhatIsAlreadyConsumed()
    {
        // the question is what the week can cost, not what is left of it
        Assert.Equal(20_000, LlmCostForecast.MaxTokens([Status(20_000, consumed: 19_000)]));
    }

    [Fact]
    public void MaxTokens_IsZeroForAnEmptyRoster() => Assert.Equal(0, LlmCostForecast.MaxTokens([]));

    // ---- forecast ----

    [Fact]
    public void Expected_AveragesTheClosedWeeks()
    {
        var expected = LlmCostForecast.Expected([Week(30, 100_000, 1.00m), Week(31, 200_000, 3.00m)]);

        Assert.NotNull(expected);
        Assert.Equal(150_000, expected!.Value.Tokens);
        Assert.Equal(2.00m, expected.Value.CostUsd);
        Assert.Equal(2, expected.Value.Weeks);
    }

    [Fact]
    public void Expected_LeavesTheRunningWeekOut()
    {
        var weeks = new[] { Week(30, 100_000, 2.00m), Week(31, 100_000, 2.00m), Week(32, 4_000, 0.08m, running: true) };

        var expected = LlmCostForecast.Expected(weeks);

        // counting a two-day-old week would drag the forecast down every Monday and let it climb back all week
        Assert.Equal(2.00m, expected!.Value.CostUsd);
        Assert.Equal(2, expected.Value.Weeks);
    }

    [Fact]
    public void Expected_IsNullWhileOnlyTheRunningWeekExists()
        => Assert.Null(LlmCostForecast.Expected([Week(32, 4_000, 0.08m, running: true)]));

    [Fact]
    public void Expected_IsNullWithoutAnyHistory()
        => Assert.Null(LlmCostForecast.Expected([]));

    [Fact]
    public void Expected_CountsAQuietWeekAsZero()
    {
        var expected = LlmCostForecast.Expected([Week(30, 0, 0m), Week(31, 100_000, 2.00m)]);

        Assert.Equal(1.00m, expected!.Value.CostUsd);
        Assert.Equal(50_000, expected.Value.Tokens);
    }
}
