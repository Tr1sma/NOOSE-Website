using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The token arithmetic behind every quota figure; the carry cap is what makes hoarding impossible.</summary>
public class LlmQuotaMathTests
{
    [Theory]
    [InlineData(0.01, 1_000)]      // one cent is a thousand quota tokens, by definition
    [InlineData(0.0247, 2_470)]
    [InlineData(2.00, 200_000)]
    [InlineData(1.0, 100_000)]
    public void FromCost_ConvertsUsdToQuotaTokens(decimal costUsd, long expected)
        => Assert.Equal(expected, LlmQuotaMath.FromCost(costUsd));

    [Fact]
    public void FromCost_RoundsAwayFromZero()
    {
        Assert.Equal(3L, LlmQuotaMath.FromCost(0.000025m)); // 2,5 → 3, not to even
        Assert.Equal(2L, LlmQuotaMath.FromCost(0.0000151m));
        Assert.Equal(1L, LlmQuotaMath.FromCost(0.0000149m));
    }

    [Fact]
    public void FromCost_NeverChargesZeroForANonFreeCall()
        => Assert.Equal(1L, LlmQuotaMath.FromCost(0.0000000001m));

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void FromCost_OfZeroOrNegative_IsZero(decimal costUsd)
        => Assert.Equal(0L, LlmQuotaMath.FromCost(costUsd));

    [Fact]
    public void FromCost_FallsBackToTheTokenFloor_WhenTheProviderReportsNoCost()
    {
        // 120 tokens at the fallback rate; without the floor the row would be free and the week would never fill,
        // taking the per-turn ceiling and the cost-spike rule down with it
        var tokens = LlmQuotaMath.FromCost(0m, 100, 20, null);

        Assert.True(tokens > 0);
        Assert.Equal(LlmQuotaMath.FromCost(120 * LlmQuotaMath.FallbackUsdPerMillionTokens / 1_000_000m), tokens);
    }

    [Fact]
    public void FromCost_PrefersTheReportedCost_WhenItIsHigherThanTheFloor()
        => Assert.Equal(1_230L, LlmQuotaMath.FromCost(0.0123m, 100, 20, null));

    [Fact]
    public void FromCost_UsesTheFloor_WhenTheReportedCostIsImplausiblyLow()
    {
        // max, not fallback: a route reporting a token amount of cost must not slip past the same floor
        var price = new LlmModelPrice { InputPerMillionUsd = 1m, OutputPerMillionUsd = 2m };

        var tokens = LlmQuotaMath.FromCost(0.0000001m, 1_000_000, 1_000_000, price);

        Assert.Equal(LlmQuotaMath.FromCost(3m), tokens);
    }

    [Fact]
    public void FromCost_WithAModelPrice_ChargesInputAndOutputSeparately()
    {
        var price = new LlmModelPrice { InputPerMillionUsd = 0.5m, OutputPerMillionUsd = 1.5m };

        var tokens = LlmQuotaMath.FromCost(0m, 2_000_000, 1_000_000, price);

        Assert.Equal(LlmQuotaMath.FromCost(1m + 1.5m), tokens);
    }

    [Fact]
    public void FromCost_OfAFreeCallWithNoTokens_StaysZero()
        => Assert.Equal(0L, LlmQuotaMath.FromCost(0m, 0, 0, null));

    [Fact]
    public void ToCost_AndToCents_RoundTrip()
    {
        Assert.Equal(0.01m, LlmQuotaMath.ToCost(1_000));
        Assert.Equal(1m, LlmQuotaMath.ToCents(1_000));
        Assert.Equal(20m, LlmQuotaMath.ToCents(20_000));
        Assert.Equal(50_000L, LlmQuotaMath.FromCost(LlmQuotaMath.ToCost(50_000)));
    }

    [Fact]
    public void CarryOut_IsThePercentOfTheUnusedRest()
        => Assert.Equal(5_000L, LlmQuotaMath.CarryOut(baseWeekly: 50_000, carryIn: 0, consumed: 30_000, carryPercent: 25));

    [Fact]
    public void CarryOut_IsCappedAtThePercentOfTheBase()
    {
        // rest is 62.500, so an uncapped share would be 15.625 — the cap holds it at the base's 25 %
        Assert.Equal(12_500L, LlmQuotaMath.CarryOut(baseWeekly: 50_000, carryIn: 12_500, consumed: 0, carryPercent: 25));
        Assert.Equal(LlmQuotaMath.CarryCap(50_000, 25), LlmQuotaMath.CarryOut(50_000, 12_500, 0, 25));
    }

    [Fact]
    public void CarryOut_IsZero_WhenTheRestIsNegative()
        => Assert.Equal(0L, LlmQuotaMath.CarryOut(baseWeekly: 50_000, carryIn: 0, consumed: 60_000, carryPercent: 50));

    [Theory]
    [InlineData(50_000, 0, 0, 0)]      // rank carries nothing forward
    [InlineData(0, 0, 0, 50)]          // unranked agent has no base to share
    public void CarryOut_IsZero_WithoutABaseOrAPercentage(long baseWeekly, long carryIn, long consumed, int percent)
        => Assert.Equal(0L, LlmQuotaMath.CarryOut(baseWeekly, carryIn, consumed, percent));

    [Fact]
    public void CarryOut_Truncates_InsteadOfRoundingUp()
        => Assert.Equal(1L, LlmQuotaMath.CarryOut(baseWeekly: 3, carryIn: 0, consumed: 0, carryPercent: 50));

    [Fact]
    public void Ceiling_IsNeverExceeded_OverTwentyIdleWeeks()
    {
        const long baseWeekly = 50_000L;
        const int percent = 50;
        var ceiling = LlmQuotaMath.Ceiling(baseWeekly, percent);
        var carry = 0L;

        for (var week = 1; week <= 20; week++)
        {
            carry = LlmQuotaMath.CarryOut(baseWeekly, carry, consumed: 0, percent);
            Assert.True(baseWeekly + carry <= ceiling, $"week {week} exceeded the ceiling");
        }

        // the fixed point is reached after a single unused week and never moves again
        Assert.Equal(LlmQuotaMath.CarryCap(baseWeekly, percent), carry);
        Assert.Equal(75_000L, baseWeekly + carry);
    }

    [Theory]
    [InlineData(20_000, 0)]
    [InlineData(35_000, 0)]
    [InlineData(50_000, 25)]
    [InlineData(80_000, 50)]
    [InlineData(120_000, 50)]
    [InlineData(200_000, 50)]
    public void Ceiling_MatchesBasePlusCap(long baseWeekly, int percent)
        => Assert.Equal(baseWeekly + LlmQuotaMath.CarryCap(baseWeekly, percent), LlmQuotaMath.Ceiling(baseWeekly, percent));

    [Theory]
    [InlineData(20_000, 40, 8_000)]
    [InlineData(200_000, 40, 80_000)]
    [InlineData(20_000, 0, 0)]        // switched off
    [InlineData(0, 40, 0)]            // no quota at all means no daily ceiling to compute
    [InlineData(100, 100, 100)]
    public void DailyLimit_IsAShareOfTheBase(long baseWeekly, int percent, long expected)
        => Assert.Equal(expected, LlmQuotaMath.DailyLimit(baseWeekly, percent));

    [Fact]
    public void ClampCarryIn_HoldsAStoredCarryToTheCurrentCap()
    {
        // the rank's base was lowered after the week was frozen; the old carry must not survive it
        Assert.Equal(5_000L, LlmQuotaMath.ClampCarryIn(storedCarryOut: 40_000, baseWeekly: 20_000, carryPercent: 25));
        Assert.Equal(4_000L, LlmQuotaMath.ClampCarryIn(4_000, 20_000, 25));
        Assert.Equal(0L, LlmQuotaMath.ClampCarryIn(-100, 20_000, 25));
        Assert.Equal(0L, LlmQuotaMath.ClampCarryIn(40_000, 20_000, 0));
    }
}
