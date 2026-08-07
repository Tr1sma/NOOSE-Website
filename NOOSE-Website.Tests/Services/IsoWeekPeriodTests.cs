using System.Globalization;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>ISO-week arithmetic behind every quota figure; the year boundary is where naive week maths breaks.</summary>
public class IsoWeekPeriodTests
{
    [Fact]
    public void Current_MatchesTheIsoWeekOfNow()
    {
        var now = DateTime.Now;
        Assert.Equal((ISOWeek.GetYear(now), ISOWeek.GetWeekOfYear(now)), IsoWeekPeriod.Current());
    }

    [Theory]
    [InlineData("2027-01-01", 2026, 53)] // a Friday whose week still belongs to the previous ISO year
    [InlineData("2026-01-01", 2026, 1)]
    [InlineData("2025-12-29", 2026, 1)]  // week 1 opens in the previous calendar year
    [InlineData("2026-08-07", 2026, 32)]
    public void From_UsesTheIsoYear_NotTheCalendarYear(string date, int year, int week)
        => Assert.Equal((year, week), IsoWeekPeriod.From(DateTime.Parse(date, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(2026, 53)] // 1 January is a Thursday
    [InlineData(2020, 53)] // leap year starting on a Wednesday
    [InlineData(2025, 52)]
    [InlineData(2027, 52)]
    public void WeeksInYear_KnowsTheLongYears(int year, int expected)
        => Assert.Equal(expected, IsoWeekPeriod.WeeksInYear(year));

    [Theory]
    [InlineData(2026, 52, 2026, 53)] // must not roll over at 52 in a 53-week year
    [InlineData(2026, 53, 2027, 1)]
    [InlineData(2027, 52, 2028, 1)]
    [InlineData(2026, 1, 2026, 2)]
    public void Next_AsksTheYearHowManyWeeksItHas(int year, int week, int expectedYear, int expectedWeek)
        => Assert.Equal((expectedYear, expectedWeek), IsoWeekPeriod.Next(year, week));

    [Theory]
    [InlineData(2027, 1, 2026, 53)]
    [InlineData(2026, 1, 2025, 52)]
    [InlineData(2026, 53, 2026, 52)]
    public void Previous_LandsOnThePredecessorYearsLastWeek(int year, int week, int expectedYear, int expectedWeek)
        => Assert.Equal((expectedYear, expectedWeek), IsoWeekPeriod.Previous(year, week));

    [Fact]
    public void NextAndPrevious_AreInverse()
    {
        for (var year = 2023; year <= 2031; year++)
        {
            for (var week = 1; week <= IsoWeekPeriod.WeeksInYear(year); week++)
            {
                var (nextYear, nextWeek) = IsoWeekPeriod.Next(year, week);
                Assert.Equal((year, week), IsoWeekPeriod.Previous(nextYear, nextWeek));
            }
        }
    }

    [Fact]
    public void IsBefore_OrdersAcrossTheYearBoundary()
    {
        Assert.True(IsoWeekPeriod.IsBefore(2026, 53, 2027, 1));
        Assert.False(IsoWeekPeriod.IsBefore(2027, 1, 2026, 53));
        Assert.False(IsoWeekPeriod.IsBefore(2026, 32, 2026, 32));
        Assert.True(IsoWeekPeriod.IsBefore(2026, 31, 2026, 32));
    }

    [Fact]
    public void Start_IsAlwaysAMonday()
    {
        for (var year = 2023; year <= 2031; year++)
        {
            for (var week = 1; week <= IsoWeekPeriod.WeeksInYear(year); week++)
            {
                Assert.Equal(DayOfWeek.Monday, IsoWeekPeriod.Start(year, week).DayOfWeek);
            }
        }
    }

    [Fact]
    public void Start_OfWeekOne_MayLieInThePreviousCalendarYear()
        => Assert.Equal(new DateTime(2025, 12, 29), IsoWeekPeriod.Start(2026, 1));

    [Fact]
    public void Start_ClampsAWeekTheYearDoesNotHave()
        => Assert.Equal(IsoWeekPeriod.Start(2027, 52), IsoWeekPeriod.Start(2027, 53));

    [Fact]
    public void Reset_IsSevenDaysAfterStart_AndCrossesTheYear()
    {
        Assert.Equal(new DateTime(2027, 1, 4), IsoWeekPeriod.Reset(2026, 53));
        Assert.Equal(IsoWeekPeriod.Start(2027, 1), IsoWeekPeriod.Reset(2026, 53));
    }

    [Fact]
    public void Reset_OpensTheDirectSuccessor()
    {
        for (var year = 2024; year <= 2029; year++)
        {
            for (var week = 1; week <= IsoWeekPeriod.WeeksInYear(year); week++)
            {
                var (nextYear, nextWeek) = IsoWeekPeriod.Next(year, week);
                Assert.Equal(IsoWeekPeriod.Start(nextYear, nextWeek), IsoWeekPeriod.Reset(year, week));
            }
        }
    }

    [Fact]
    public void Label_FormatsGerman()
    {
        Assert.Equal("KW 07/2026", IsoWeekPeriod.Label(2026, 7));
        Assert.Equal("KW 53/2026", IsoWeekPeriod.Label(2026, 53));
    }
}
