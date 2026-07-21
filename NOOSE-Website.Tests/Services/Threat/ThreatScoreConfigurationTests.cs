using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using Xunit;

namespace NOOSE_Website.Tests.Services.Threat;

public class ThreatScoreConfigurationTests
{
    private const int Precision = 6;

    // ---------------------------------------------------------------
    // Default()
    // ---------------------------------------------------------------

    [Fact]
    public void Default_returns_shared_defaults()
    {
        var d = ThreatScoreConfiguration.Default();

        // shared
        Assert.Equal(90.0, d.HalfLifeDays, Precision);
        Assert.Equal(180, d.ConfidenceFreshDays);
        Assert.Equal(50, d.TriageThreshold);
        Assert.Equal(3.0, d.KindWeightHeavy, Precision);
        Assert.Equal(2.0, d.KindWeightMedium, Precision);
        Assert.Equal(1.0, d.KindWeightLight, Precision);
        Assert.Equal(2.0, d.OutcomeShot, Precision);
        Assert.Equal(1.5, d.OutcomeInjection, Precision);
        Assert.Equal(1.2, d.OutcomeRunningStill, Precision);
        Assert.Equal(1.0, d.OutcomeReleased, Precision);
    }

    [Fact]
    public void Default_faction_and_person_caps_sum_to_100()
    {
        var d = ThreatScoreConfiguration.Default();

        Assert.Equal(100.0, d.CapS1 + d.CapS2 + d.CapS3 + d.CapS4, Precision);
        Assert.Equal(100.0, d.CapP1 + d.CapP2 + d.CapP3 + d.CapP4 + d.CapP5, Precision);
    }

    [Fact]
    public void Default_S2_and_P2_subcaps_sum_to_their_group_cap()
    {
        var d = ThreatScoreConfiguration.Default();

        var s2Sub = d.CapSize + d.RanksMaxPoints + d.LeadPoints + d.EstatePoints + d.CapWeapons + d.CapInfra;
        Assert.Equal(d.CapS2, s2Sub, Precision);

        var p2Sub = d.PersonCapWeapons + d.FugitivePoints;
        Assert.Equal(d.CapP2, p2Sub, Precision);
    }

    [Fact]
    public void Default_returns_a_new_instance_each_call()
    {
        var a = ThreatScoreConfiguration.Default();
        var b = ThreatScoreConfiguration.Default();

        Assert.NotSame(a, b);
    }

    // ---------------------------------------------------------------
    // Balance() — faction caps S1..S4 -> 100
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(55.0, 22.0, 15.0, 8.0)]  // defaults (already balanced)
    [InlineData(10.0, 10.0, 10.0, 10.0)] // equal
    [InlineData(1.0, 2.0, 3.0, 4.0)]     // arbitrary
    [InlineData(0.0, 50.0, 0.0, 0.0)]    // single non-zero
    [InlineData(100.0, 0.0, 0.0, 0.0)]   // single non-zero (already 100)
    [InlineData(7.0, 7.0, 7.0, 79.0)]    // skewed
    public void Balance_normalizes_faction_caps_to_100(double s1, double s2, double s3, double s4)
    {
        var cfg = new ThreatScoreConfiguration { CapS1 = s1, CapS2 = s2, CapS3 = s3, CapS4 = s4 };

        cfg.Balance();

        Assert.Equal(100.0, cfg.CapS1 + cfg.CapS2 + cfg.CapS3 + cfg.CapS4, Precision);
    }

    [Fact]
    public void Balance_equal_faction_caps_split_evenly()
    {
        var cfg = new ThreatScoreConfiguration { CapS1 = 10, CapS2 = 10, CapS3 = 10, CapS4 = 10 };

        cfg.Balance();

        Assert.Equal(25.0, cfg.CapS1, Precision);
        Assert.Equal(25.0, cfg.CapS2, Precision);
        Assert.Equal(25.0, cfg.CapS3, Precision);
        Assert.Equal(25.0, cfg.CapS4, Precision);
    }

    [Fact]
    public void Balance_preserves_relative_faction_weights()
    {
        // 20:10:10:10 -> should scale to same ratios summing 100
        var cfg = new ThreatScoreConfiguration { CapS1 = 20, CapS2 = 10, CapS3 = 10, CapS4 = 10 };

        cfg.Balance();

        Assert.Equal(40.0, cfg.CapS1, Precision);
        Assert.Equal(20.0, cfg.CapS2, Precision);
        Assert.Equal(20.0, cfg.CapS3, Precision);
        Assert.Equal(20.0, cfg.CapS4, Precision);
    }

    [Fact]
    public void Balance_allZero_faction_caps_falls_back_to_defaults()
    {
        var cfg = new ThreatScoreConfiguration { CapS1 = 0, CapS2 = 0, CapS3 = 0, CapS4 = 0 };

        cfg.Balance();

        Assert.Equal(55.0, cfg.CapS1, Precision);
        Assert.Equal(22.0, cfg.CapS2, Precision);
        Assert.Equal(15.0, cfg.CapS3, Precision);
        Assert.Equal(8.0, cfg.CapS4, Precision);
    }

    // ---------------------------------------------------------------
    // Balance() — person caps P1..P5 -> 100
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(40.0, 22.0, 18.0, 12.0, 8.0)] // defaults
    [InlineData(1.0, 1.0, 1.0, 1.0, 1.0)]     // equal
    [InlineData(2.0, 4.0, 6.0, 8.0, 10.0)]    // arbitrary
    [InlineData(0.0, 0.0, 100.0, 0.0, 0.0)]   // single non-zero
    public void Balance_normalizes_person_caps_to_100(double p1, double p2, double p3, double p4, double p5)
    {
        var cfg = new ThreatScoreConfiguration { CapP1 = p1, CapP2 = p2, CapP3 = p3, CapP4 = p4, CapP5 = p5 };

        cfg.Balance();

        Assert.Equal(100.0, cfg.CapP1 + cfg.CapP2 + cfg.CapP3 + cfg.CapP4 + cfg.CapP5, Precision);
    }

    [Fact]
    public void Balance_allZero_person_caps_falls_back_to_defaults()
    {
        var cfg = new ThreatScoreConfiguration { CapP1 = 0, CapP2 = 0, CapP3 = 0, CapP4 = 0, CapP5 = 0 };

        cfg.Balance();

        Assert.Equal(40.0, cfg.CapP1, Precision);
        Assert.Equal(22.0, cfg.CapP2, Precision);
        Assert.Equal(18.0, cfg.CapP3, Precision);
        Assert.Equal(12.0, cfg.CapP4, Precision);
        Assert.Equal(8.0, cfg.CapP5, Precision);
    }

    // ---------------------------------------------------------------
    // Balance() — S2 sub-caps -> CapS2
    // ---------------------------------------------------------------

    [Theory]
    // subs (CapSize, Ranks, Lead, Estate, Weapons, Infra); faction caps left at default so CapS2 == 22
    [InlineData(10, 3, 2.0, 1.0, 3.0, 3.0)]  // defaults (balanced)
    [InlineData(20, 6, 4.0, 2.0, 6.0, 6.0)]  // 2x defaults -> halved
    [InlineData(10, 5, 2.0, 1.0, 3.0, 3.0)]  // triggers ranks floor + residue
    [InlineData(1, 1, 1.0, 1.0, 1.0, 1.0)]   // all equal-ish small
    public void Balance_normalizes_S2_subcaps_to_CapS2(
        double capSize, int ranks, double lead, double estate, double weapons, double infra)
    {
        var cfg = new ThreatScoreConfiguration
        {
            CapSize = capSize,
            RanksMaxPoints = ranks,
            LeadPoints = lead,
            EstatePoints = estate,
            CapWeapons = weapons,
            CapInfra = infra,
        };

        cfg.Balance();

        var sub = cfg.CapSize + cfg.RanksMaxPoints + cfg.LeadPoints + cfg.EstatePoints + cfg.CapWeapons + cfg.CapInfra;
        Assert.Equal(cfg.CapS2, sub, Precision);
        Assert.Equal(22.0, cfg.CapS2, Precision); // faction caps untouched -> CapS2 stays default
    }

    [Fact]
    public void Balance_S2_RanksMaxPoints_is_floored_not_rounded()
    {
        // sub = 24, f = 22/24 = 0.9166..; 5 * f = 4.583.. -> floor -> 4
        var cfg = new ThreatScoreConfiguration
        {
            CapSize = 10,
            RanksMaxPoints = 5,
            LeadPoints = 2,
            EstatePoints = 1,
            CapWeapons = 3,
            CapInfra = 3,
        };

        cfg.Balance();

        Assert.Equal(4, cfg.RanksMaxPoints);
        var sub = cfg.CapSize + cfg.RanksMaxPoints + cfg.LeadPoints + cfg.EstatePoints + cfg.CapWeapons + cfg.CapInfra;
        Assert.Equal(cfg.CapS2, sub, Precision);
    }

    [Fact]
    public void Balance_allZero_S2_subcaps_reset_then_normalize_to_CapS2()
    {
        // all S2 subs zero, faction caps default -> reset to defaults, CapS2 == 22 leaves them unchanged
        var cfg = new ThreatScoreConfiguration
        {
            CapSize = 0,
            RanksMaxPoints = 0,
            LeadPoints = 0,
            EstatePoints = 0,
            CapWeapons = 0,
            CapInfra = 0,
        };

        cfg.Balance();

        Assert.Equal(10.0, cfg.CapSize, Precision);
        Assert.Equal(3, cfg.RanksMaxPoints);
        Assert.Equal(2.0, cfg.LeadPoints, Precision);
        Assert.Equal(1.0, cfg.EstatePoints, Precision);
        Assert.Equal(3.0, cfg.CapWeapons, Precision);
        Assert.Equal(3.0, cfg.CapInfra, Precision);
    }

    [Fact]
    public void Balance_faction_change_propagates_to_S2_subcap_target()
    {
        // equal faction caps -> CapS2 becomes 25; S2 subs must re-sum to the new CapS2
        var cfg = new ThreatScoreConfiguration { CapS1 = 10, CapS2 = 10, CapS3 = 10, CapS4 = 10 };

        cfg.Balance();

        Assert.Equal(25.0, cfg.CapS2, Precision);
        var sub = cfg.CapSize + cfg.RanksMaxPoints + cfg.LeadPoints + cfg.EstatePoints + cfg.CapWeapons + cfg.CapInfra;
        Assert.Equal(cfg.CapS2, sub, Precision);
    }

    // ---------------------------------------------------------------
    // Balance() — P2 sub-caps -> CapP2
    // ---------------------------------------------------------------

    [Theory]
    // (PersonCapWeapons, FugitivePoints); person caps default so CapP2 == 22
    [InlineData(14.0, 8.0)]  // defaults
    [InlineData(30.0, 10.0)] // scale down
    [InlineData(5.0, 5.0)]   // even split
    [InlineData(1.0, 3.0)]   // skewed
    public void Balance_normalizes_P2_subcaps_to_CapP2(double weapons, double fugitive)
    {
        var cfg = new ThreatScoreConfiguration { PersonCapWeapons = weapons, FugitivePoints = fugitive };

        cfg.Balance();

        Assert.Equal(cfg.CapP2, cfg.PersonCapWeapons + cfg.FugitivePoints, Precision);
        Assert.Equal(22.0, cfg.CapP2, Precision);
    }

    [Fact]
    public void Balance_allZero_P2_subcaps_puts_all_weight_on_weapons()
    {
        var cfg = new ThreatScoreConfiguration { PersonCapWeapons = 0, FugitivePoints = 0 };

        cfg.Balance();

        Assert.Equal(cfg.CapP2, cfg.PersonCapWeapons, Precision);
        Assert.Equal(0.0, cfg.FugitivePoints, Precision);
    }

    // ---------------------------------------------------------------
    // Balance() — idempotent on already-balanced defaults
    // ---------------------------------------------------------------

    [Fact]
    public void Balance_on_default_config_is_a_noop()
    {
        var cfg = ThreatScoreConfiguration.Default();
        var d = ThreatScoreConfiguration.Default();

        cfg.Balance();

        Assert.Equal(d.CapS1, cfg.CapS1, Precision);
        Assert.Equal(d.CapS2, cfg.CapS2, Precision);
        Assert.Equal(d.CapS3, cfg.CapS3, Precision);
        Assert.Equal(d.CapS4, cfg.CapS4, Precision);
        Assert.Equal(d.CapSize, cfg.CapSize, Precision);
        Assert.Equal(d.RanksMaxPoints, cfg.RanksMaxPoints);
        Assert.Equal(d.LeadPoints, cfg.LeadPoints, Precision);
        Assert.Equal(d.EstatePoints, cfg.EstatePoints, Precision);
        Assert.Equal(d.CapWeapons, cfg.CapWeapons, Precision);
        Assert.Equal(d.CapInfra, cfg.CapInfra, Precision);
        Assert.Equal(d.CapP1, cfg.CapP1, Precision);
        Assert.Equal(d.CapP2, cfg.CapP2, Precision);
        Assert.Equal(d.CapP3, cfg.CapP3, Precision);
        Assert.Equal(d.CapP4, cfg.CapP4, Precision);
        Assert.Equal(d.CapP5, cfg.CapP5, Precision);
        Assert.Equal(d.PersonCapWeapons, cfg.PersonCapWeapons, Precision);
        Assert.Equal(d.FugitivePoints, cfg.FugitivePoints, Precision);
    }

    [Fact]
    public void Balance_is_stable_when_applied_twice()
    {
        var cfg = new ThreatScoreConfiguration { CapS1 = 3, CapS2 = 9, CapS3 = 4, CapS4 = 4 };

        cfg.Balance();
        var afterFirst = (cfg.CapS1, cfg.CapS2, cfg.CapS3, cfg.CapS4);
        cfg.Balance();

        Assert.Equal(afterFirst.Item1, cfg.CapS1, Precision);
        Assert.Equal(afterFirst.Item2, cfg.CapS2, Precision);
        Assert.Equal(afterFirst.Item3, cfg.CapS3, Precision);
        Assert.Equal(afterFirst.Item4, cfg.CapS4, Precision);
        Assert.Equal(100.0, cfg.CapS1 + cfg.CapS2 + cfg.CapS3 + cfg.CapS4, Precision);
    }

    // ---------------------------------------------------------------
    // KindWeight()
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void KindWeight_null_or_whitespace_returns_light(string? kind)
    {
        var cfg = ThreatScoreConfiguration.Default();

        Assert.Equal(cfg.KindWeightLight, cfg.KindWeight(kind), Precision);
    }

    [Theory]
    [InlineData("mord")]
    [InlineData("tötung")]
    [InlineData("toetung")]
    [InlineData("hinrichtung")]
    [InlineData("geiselnahme")]
    [InlineData("entführung")]
    [InlineData("entfuehrung")]
    [InlineData("anschlag")]
    [InlineData("terror")]
    public void KindWeight_heavy_keyword_returns_heavy(string kind)
    {
        var cfg = ThreatScoreConfiguration.Default();

        Assert.Equal(cfg.KindWeightHeavy, cfg.KindWeight(kind), Precision);
    }

    [Theory]
    [InlineData("raub")]
    [InlineData("überfall")]
    [InlineData("ueberfall")]
    [InlineData("schießerei")]
    [InlineData("schiesserei")]
    [InlineData("bank")]
    [InlineData("erpressung")]
    [InlineData("schutzgeld")]
    [InlineData("waffenhandel")]
    [InlineData("drogenhandel")]
    public void KindWeight_medium_keyword_returns_medium(string kind)
    {
        var cfg = ThreatScoreConfiguration.Default();

        Assert.Equal(cfg.KindWeightMedium, cfg.KindWeight(kind), Precision);
    }

    [Theory]
    [InlineData("diebstahl")]
    [InlineData("verkehrsdelikt")]
    [InlineData("ordnungswidrigkeit")]
    [InlineData("xyz")]
    public void KindWeight_unmatched_keyword_returns_light(string kind)
    {
        var cfg = ThreatScoreConfiguration.Default();

        Assert.Equal(cfg.KindWeightLight, cfg.KindWeight(kind), Precision);
    }

    [Theory]
    [InlineData("MORD")]
    [InlineData("Mordkommission")]
    [InlineData("schwerer Raubüberfall")] // contains 'raub' (and 'überfall') -> medium
    [InlineData("TERROR-Anschlag")]
    public void KindWeight_matches_are_case_insensitive_and_substring(string kind)
    {
        var cfg = ThreatScoreConfiguration.Default();

        var result = cfg.KindWeight(kind);
        Assert.True(result == cfg.KindWeightHeavy || result == cfg.KindWeightMedium);
    }

    [Fact]
    public void KindWeight_heavy_takes_priority_over_medium()
    {
        var cfg = ThreatScoreConfiguration.Default();

        // contains both 'raub' (medium) and 'mord' (heavy) -> heavy wins
        Assert.Equal(cfg.KindWeightHeavy, cfg.KindWeight("Raubmord"), Precision);
    }

    [Fact]
    public void KindWeight_uses_configured_weights()
    {
        var cfg = new ThreatScoreConfiguration
        {
            KindWeightHeavy = 99.0,
            KindWeightMedium = 55.0,
            KindWeightLight = 11.0,
        };

        Assert.Equal(99.0, cfg.KindWeight("terror"), Precision);
        Assert.Equal(55.0, cfg.KindWeight("raub"), Precision);
        Assert.Equal(11.0, cfg.KindWeight("spazieren"), Precision);
        Assert.Equal(11.0, cfg.KindWeight(null), Precision);
    }

    // ---------------------------------------------------------------
    // OutcomeWeight()
    // ---------------------------------------------------------------

    [Fact]
    public void OutcomeWeight_maps_each_outcome_to_default_weight()
    {
        var cfg = ThreatScoreConfiguration.Default();

        Assert.Equal(2.0, cfg.OutcomeWeight(MeasureOutcome.Shot), Precision);
        Assert.Equal(1.5, cfg.OutcomeWeight(MeasureOutcome.Injection), Precision);
        Assert.Equal(1.2, cfg.OutcomeWeight(MeasureOutcome.RunningStill), Precision);
        Assert.Equal(1.0, cfg.OutcomeWeight(MeasureOutcome.OfficiallyReleased), Precision);
    }

    [Fact]
    public void OutcomeWeight_undefined_value_falls_back_to_released()
    {
        var cfg = ThreatScoreConfiguration.Default();

        Assert.Equal(cfg.OutcomeReleased, cfg.OutcomeWeight((MeasureOutcome)999), Precision);
    }

    [Fact]
    public void OutcomeWeight_uses_configured_weights()
    {
        var cfg = new ThreatScoreConfiguration
        {
            OutcomeShot = 10.0,
            OutcomeInjection = 8.0,
            OutcomeRunningStill = 6.0,
            OutcomeReleased = 4.0,
        };

        Assert.Equal(10.0, cfg.OutcomeWeight(MeasureOutcome.Shot), Precision);
        Assert.Equal(8.0, cfg.OutcomeWeight(MeasureOutcome.Injection), Precision);
        Assert.Equal(6.0, cfg.OutcomeWeight(MeasureOutcome.RunningStill), Precision);
        Assert.Equal(4.0, cfg.OutcomeWeight(MeasureOutcome.OfficiallyReleased), Precision);
    }
}
