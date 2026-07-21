using MudBlazor;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Models;

public class RecencyAssessmentTests
{
    private static readonly DateTime Now = new(2026, 07, 21, 12, 0, 0, DateTimeKind.Utc);

    private static RecencyLevel LevelForAge(int warningDays, int staleDays, double ageDays)
    {
        var referenceDate = Now - TimeSpan.FromDays(ageDays);
        return RecencyAssessment.Level(warningDays, staleDays, referenceDate, Now);
    }

    // warning=30, stale=90 ladder across the three arms
    [Theory]
    [InlineData(0.0, RecencyLevel.Fresh)]
    [InlineData(1.0, RecencyLevel.Fresh)]
    [InlineData(29.0, RecencyLevel.Fresh)]
    [InlineData(29.999, RecencyLevel.Fresh)]
    [InlineData(30.0, RecencyLevel.Warning)]   // exactly on warning threshold
    [InlineData(31.0, RecencyLevel.Warning)]
    [InlineData(60.0, RecencyLevel.Warning)]
    [InlineData(89.0, RecencyLevel.Warning)]
    [InlineData(89.999, RecencyLevel.Warning)] // just under stale threshold
    [InlineData(90.0, RecencyLevel.Stale)]     // exactly on stale threshold
    [InlineData(91.0, RecencyLevel.Stale)]
    [InlineData(365.0, RecencyLevel.Stale)]
    public void Level_maps_age_to_level_by_thresholds(double ageDays, RecencyLevel expected)
    {
        Assert.Equal(expected, LevelForAge(30, 90, ageDays));
    }

    [Fact]
    public void Level_ageExactlyOnWarning_returnsWarning()
    {
        Assert.Equal(RecencyLevel.Warning, LevelForAge(30, 90, 30.0));
    }

    [Fact]
    public void Level_ageExactlyOnStale_returnsStale()
    {
        Assert.Equal(RecencyLevel.Stale, LevelForAge(30, 90, 90.0));
    }

    [Fact]
    public void Level_ageOneDayBelowWarning_returnsFresh()
    {
        Assert.Equal(RecencyLevel.Fresh, LevelForAge(30, 90, 29.0));
    }

    [Fact]
    public void Level_ageOneDayBelowStale_returnsWarning()
    {
        Assert.Equal(RecencyLevel.Warning, LevelForAge(30, 90, 89.0));
    }

    [Fact]
    public void Level_referenceDateInFuture_returnsFresh()
    {
        // now before referenceDate => negative age => below every threshold
        Assert.Equal(RecencyLevel.Fresh, LevelForAge(30, 90, -5.0));
    }

    [Fact]
    public void Level_referenceEqualsNow_returnsFresh()
    {
        Assert.Equal(RecencyLevel.Fresh, RecencyAssessment.Level(30, 90, Now, Now));
    }

    // Stale is checked first: when age is exactly staleDays it wins even though
    // it also satisfies the warning predicate.
    [Fact]
    public void Level_staleTakesPrecedenceOverWarning()
    {
        Assert.Equal(RecencyLevel.Stale, LevelForAge(30, 90, 90.0));
    }

    // Zero thresholds: any non-negative age is immediately Stale.
    [Theory]
    [InlineData(0.0, RecencyLevel.Stale)]
    [InlineData(0.5, RecencyLevel.Stale)]
    [InlineData(100.0, RecencyLevel.Stale)]
    public void Level_zeroStaleThreshold_returnsStaleForNonNegativeAge(double ageDays, RecencyLevel expected)
    {
        Assert.Equal(expected, LevelForAge(0, 0, ageDays));
    }

    [Fact]
    public void Level_zeroStaleThreshold_negativeAge_returnsFresh()
    {
        // age < 0 fails both >= 0 predicates
        Assert.Equal(RecencyLevel.Fresh, LevelForAge(0, 0, -1.0));
    }

    // warningDays == staleDays: no Warning band exists.
    [Theory]
    [InlineData(29.0, RecencyLevel.Fresh)]
    [InlineData(30.0, RecencyLevel.Stale)]
    [InlineData(31.0, RecencyLevel.Stale)]
    public void Level_equalWarningAndStaleThresholds_hasNoWarningBand(double ageDays, RecencyLevel expected)
    {
        Assert.Equal(expected, LevelForAge(30, 30, ageDays));
    }

    // Zero warning threshold: non-negative age below stale is Warning.
    [Theory]
    [InlineData(0.0, RecencyLevel.Warning)]
    [InlineData(5.0, RecencyLevel.Warning)]
    [InlineData(90.0, RecencyLevel.Stale)]
    public void Level_zeroWarningThreshold_returnsWarningBelowStale(double ageDays, RecencyLevel expected)
    {
        Assert.Equal(expected, LevelForAge(0, 90, ageDays));
    }

    // Fractional-day age below the whole-day warning threshold stays Fresh.
    [Fact]
    public void Level_subDayAge_belowWarning_returnsFresh()
    {
        var referenceDate = Now - TimeSpan.FromHours(6);
        Assert.Equal(RecencyLevel.Fresh, RecencyAssessment.Level(1, 7, referenceDate, Now));
    }

    // ----- RecencyLevelDisplay.Name -----

    [Theory]
    [InlineData(RecencyLevel.Fresh, "Aktuell")]
    [InlineData(RecencyLevel.Warning, "Wird älter")]
    [InlineData(RecencyLevel.Stale, "Veraltet")]
    public void Name_returnsGermanLabelPerLevel(RecencyLevel level, string expected)
    {
        Assert.Equal(expected, RecencyLevelDisplay.Name(level));
    }

    [Fact]
    public void Name_unknownLevel_returnsUnbekannt()
    {
        Assert.Equal("Unbekannt", RecencyLevelDisplay.Name((RecencyLevel)99));
    }

    // ----- RecencyLevelDisplay.Colour -----

    [Theory]
    [InlineData(RecencyLevel.Fresh, Color.Success)]
    [InlineData(RecencyLevel.Warning, Color.Warning)]
    [InlineData(RecencyLevel.Stale, Color.Error)]
    public void Colour_returnsMudColorPerLevel(RecencyLevel level, Color expected)
    {
        Assert.Equal(expected, RecencyLevelDisplay.Colour(level));
    }

    [Fact]
    public void Colour_unknownLevel_returnsDefault()
    {
        Assert.Equal(Color.Default, RecencyLevelDisplay.Colour((RecencyLevel)99));
    }
}
