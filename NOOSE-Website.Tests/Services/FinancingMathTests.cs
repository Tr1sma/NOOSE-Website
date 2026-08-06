using NOOSE_Website.Data.Entities.Financing;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The money arithmetic behind every funding figure; rounding happens per line, never on the sum.</summary>
public class FinancingMathTests
{
    [Theory]
    [InlineData(1000, 3, 3000)]
    [InlineData(0, 5, 0)]
    [InlineData(1500, 1, 1500)]
    public void Gross_MultipliesUnitPriceByQuantity(decimal unitPrice, int quantity, decimal expected)
        => Assert.Equal(expected, FinancingMath.Gross(unitPrice, quantity));

    [Theory]
    [InlineData(10_000, 1, 100, 10_000)]
    [InlineData(10_000, 1, 70, 7_000)]
    [InlineData(10_000, 2, 50, 10_000)]
    [InlineData(10_000, 1, 1, 100)]
    public void Subsidy_AppliesThePercentage(decimal unitPrice, int quantity, int percent, decimal expected)
        => Assert.Equal(expected, FinancingMath.Subsidy(unitPrice, quantity, percent));

    [Fact]
    public void Subsidy_RoundsHalfAwayFromZero()
    {
        // 3 × 3.333 = 9.999, 70 % = 6.999,30 → 6.999
        Assert.Equal(6_999m, FinancingMath.Subsidy(3_333m, 3, 70));
        // 1 × 5 = 5, 50 % = 2,50 → 3 (away from zero, not to even)
        Assert.Equal(3m, FinancingMath.Subsidy(5m, 1, 50));
        // 1 × 15 = 15, 50 % = 7,50 → 8
        Assert.Equal(8m, FinancingMath.Subsidy(15m, 1, 50));
    }

    [Fact]
    public void Subsidy_ZeroQuantity_IsZero()
        => Assert.Equal(0m, FinancingMath.Subsidy(10_000m, 0, 100));

    [Fact]
    public void OwnShare_IsTheRemainderOfTheRoundedSubsidy()
    {
        Assert.Equal(3_000m, FinancingMath.OwnShare(10_000m, 1, 70));
        Assert.Equal(0m, FinancingMath.OwnShare(10_000m, 1, 100));
    }

    [Fact]
    public void SubsidyTotal_SumsRoundedLines_NotARoundedSum()
    {
        var lines = new List<FinancingRequestLine>
        {
            new() { UnitPrice = 5m, Quantity = 1, SubsidyPercent = 50 },
            new() { UnitPrice = 5m, Quantity = 1, SubsidyPercent = 50 },
        };
        // each line rounds 2,50 → 3, so the total is 6, not the 5 a rounded sum would give
        Assert.Equal(6m, FinancingMath.SubsidyTotal(lines, l => l.Quantity));
    }

    [Fact]
    public void EffectiveQuantity_PrefersTheApprovedQuantity()
    {
        var line = new FinancingRequestLine { UnitPrice = 100m, Quantity = 5, SubsidyPercent = 100 };
        Assert.Equal(5, FinancingMath.EffectiveQuantity(line));
        Assert.Equal(500m, FinancingMath.EffectiveSubsidy(line));

        line.ApprovedQuantity = 2;
        Assert.Equal(2, FinancingMath.EffectiveQuantity(line));
        Assert.Equal(200m, FinancingMath.EffectiveSubsidy(line));
        // the requested figures stay readable after a cut
        Assert.Equal(500m, FinancingMath.RequestedSubsidy(line));
        Assert.Equal(500m, FinancingMath.RequestedGross(line));

        line.ApprovedQuantity = 0;
        Assert.Equal(0m, FinancingMath.EffectiveSubsidy(line));
    }

    [Theory]
    [InlineData(1000, 50, 500)]
    [InlineData(1000, 0, 0)]
    [InlineData(1000, 100, 1000)]
    [InlineData(999, 50, 500)]
    public void CarryOut_TakesTheRankShareOfTheRest(decimal remaining, int percent, decimal expected)
        => Assert.Equal(expected, FinancingMath.CarryOut(remaining, percent));

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-500, 50)]
    public void CarryOut_NeverCarriesADeficit(decimal remaining, int percent)
        => Assert.Equal(0m, FinancingMath.CarryOut(remaining, percent));
}
