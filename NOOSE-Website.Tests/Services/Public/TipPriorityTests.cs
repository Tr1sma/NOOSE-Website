using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

public class TipPriorityTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 2)]
    [InlineData(4_999, 2)]
    [InlineData(5_000, 3)]
    [InlineData(24_999, 3)]
    [InlineData(25_000, 4)]
    [InlineData(99_999, 4)]
    [InlineData(100_000, 5)]
    [InlineData(10_000_000, 5)]
    public void The_bounty_band_rises_in_five_steps(int total, int expected)
        => Assert.Equal(expected, TipPriority.BountyBand(total));

    [Theory]
    [InlineData(HazardLevel.No, 1)]
    [InlineData(HazardLevel.Low, 2)]
    [InlineData(HazardLevel.Medium, 3)]
    [InlineData(HazardLevel.High, 4)]
    [InlineData(HazardLevel.Critical, 5)]
    public void The_hazard_band_is_the_level_plus_one(HazardLevel level, int expected)
        => Assert.Equal(expected, TipPriority.HazardBand(level));

    [Fact]
    public void A_missing_hazard_level_counts_as_none()
        => Assert.Equal(TipPriority.HazardBand(HazardLevel.No), TipPriority.HazardBand(null));

    [Fact]
    public void No_factor_can_erase_the_others()
    {
        // multiplied literally, a critical notice without bounty would score zero
        var withoutBounty = TipPriority.Compute(0m, HazardLevel.Critical, 0);
        var trivial = TipPriority.Compute(1_000m, HazardLevel.No, 0);
        Assert.True(withoutBounty > trivial);
        Assert.True(withoutBounty >= TipPriority.Min);
    }

    [Fact]
    public void The_lowest_tip_scores_the_minimum()
        => Assert.Equal(TipPriority.Min, TipPriority.Compute(0m, null, 0));

    [Fact]
    public void The_highest_tip_scores_the_maximum()
        => Assert.Equal(TipPriority.Max, TipPriority.Compute(250_000m, HazardLevel.Critical, 50));

    [Fact]
    public void Every_combination_stays_inside_the_documented_range()
    {
        decimal[] bounties = [0m, 1_000m, 10_000m, 50_000m, 500_000m];
        int[] confirmed = [0, 1, 5, 40];
        foreach (var bounty in bounties)
        {
            foreach (var level in Enum.GetValues<HazardLevel>())
            {
                foreach (var tips in confirmed)
                {
                    var score = TipPriority.Compute(bounty, level, tips);
                    Assert.InRange(score, TipPriority.Min, TipPriority.Max);
                }
            }
        }
    }

    [Fact]
    public void Trust_alone_orders_two_tips_without_a_notice()
    {
        var trusted = TipPriority.Compute(0m, null, 20);
        var fresh = TipPriority.Compute(0m, null, 0);
        Assert.True(trusted > fresh);
    }

    [Fact]
    public void A_referenced_tip_outranks_an_unreferenced_one_of_the_same_tipster()
    {
        var referenced = TipPriority.Compute(30_000m, HazardLevel.High, 0);
        var free = TipPriority.Compute(0m, null, 20);
        Assert.True(referenced > free);
    }

    // ----- the capture floor -----

    [Fact]
    public void Holding_a_person_outranks_the_best_possible_observation()
    {
        // the worst possible holding report: no bounty, no hazard level, a tipster with no record
        var holding = TipPriority.Compute(0m, null, 0, TipKind.Ergreifung, TipHandover.Festgehalten);
        // and the best one the formula can produce
        var best = TipPriority.Compute(1_000_000m, HazardLevel.Critical, 50);
        Assert.True(holding >= best, $"holding {holding} must not sort below observation {best}");
        Assert.Equal(TipPriority.Max, holding);
    }

    [Fact]
    public void A_handed_over_report_outranks_an_ordinary_observation()
    {
        var handed = TipPriority.Compute(0m, null, 0, TipKind.Ergreifung, TipHandover.Uebergeben);
        var ordinary = TipPriority.Compute(30_000m, HazardLevel.High, 2);
        Assert.True(handed > ordinary, $"handed over {handed} should outrank observation {ordinary}");
    }

    [Fact]
    public void Holding_a_person_outranks_having_handed_them_over()
    {
        var holding = TipPriority.Compute(0m, null, 0, TipKind.Ergreifung, TipHandover.Festgehalten);
        var handed = TipPriority.Compute(0m, null, 0, TipKind.Ergreifung, TipHandover.Uebergeben);
        Assert.True(holding > handed);
    }

    [Fact]
    public void The_floor_lifts_but_never_overrides()
    {
        // a floor, not a fixed value: the capture of a high-bounty target still outranks the capture of a nobody
        var nobody = TipPriority.Compute(0m, null, 0, TipKind.Ergreifung, TipHandover.Uebergeben);
        var target = TipPriority.Compute(200_000m, HazardLevel.Critical, 20, TipKind.Ergreifung,
            TipHandover.Uebergeben);
        Assert.True(target > nobody);
        Assert.Equal(TipPriority.Max, target);
    }

    [Fact]
    public void An_observation_is_untouched_by_the_floor()
    {
        Assert.Equal(0, TipPriority.Floor(TipKind.Beobachtung, null));
        Assert.Equal(0, TipPriority.Floor(TipKind.Beobachtung, TipHandover.Festgehalten));
        var plain = TipPriority.Compute(5_000m, HazardLevel.Medium, 1);
        Assert.Equal(plain, TipPriority.Compute(5_000m, HazardLevel.Medium, 1, TipKind.Beobachtung));
    }

    [Fact]
    public void The_ceiling_does_not_move_with_the_floor()
    {
        // SetPriorityAsync clamps a hand-set value to Min..Max, so raising Max would widen what leadership may pin
        Assert.Equal(100, TipPriority.Max);
        Assert.True(TipPriority.Floor(TipKind.Ergreifung, TipHandover.Uebergeben) < TipPriority.Max);
        Assert.Equal(TipPriority.Max, TipPriority.Floor(TipKind.Ergreifung, TipHandover.Festgehalten));
    }
}
