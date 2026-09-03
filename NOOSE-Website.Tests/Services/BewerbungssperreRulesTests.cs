using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Unit tests for the ban rules: the 14-day duration, the active predicate and the picker's local-to-UTC step.</summary>
public class BewerbungssperreRulesTests
{
    [Fact]
    public void BanDuration_IsFourteenDays()
    {
        // spelled out rather than read from the constant: the point is to catch the constant changing
        Assert.Equal(TimeSpan.FromDays(14), BewerbungssperreRules.BanDuration);
    }

    [Fact]
    public void BannedUntil_IsFourteenDaysAfterNow()
    {
        var now = new DateTime(2026, 9, 3, 18, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 9, 17, 18, 30, 0, DateTimeKind.Utc), BewerbungssperreRules.BannedUntil(now));
    }

    [Theory]
    [InlineData(true, null, true)]      // blacklist: active without an end date
    [InlineData(false, 5, true)]        // temporary ban still running
    [InlineData(false, -1, false)]      // temporary ban expired
    [InlineData(false, null, false)]    // neither blacklist nor end date
    public void Active_MatchesBlacklistAndRunningBansOnly(bool isBlacklist, int? daysFromNow, bool expected)
    {
        var now = DateTime.UtcNow;
        var row = new Bewerbungssperre
        {
            IsBlacklist = isBlacklist,
            BannedUntil = daysFromNow is { } d ? now.AddDays(d) : null,
        };

        var matches = BewerbungssperreRules.Active(now).Compile()(row);

        Assert.Equal(expected, matches);
    }

    [Fact]
    public void PickedDateToUtc_EndsAtTheEndOfTheChosenLocalDay()
    {
        // what MudDatePicker hands over: local midnight, Kind Unspecified
        var picked = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Unspecified);

        var utc = BewerbungssperreRules.PickedDateToUtc(picked);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        // back in local time it is the last tick of the 17th, so "gesperrt bis 17.09." includes that day
        var local = utc.ToLocalTime();
        Assert.Equal(new DateTime(2026, 9, 17), local.Date);
        Assert.Equal(23, local.Hour);
        Assert.Equal(59, local.Minute);
    }

    [Fact]
    public void PickedDateToUtc_DropsAnyTimeComponentOfThePickedValue()
    {
        var noon = new DateTime(2026, 9, 17, 12, 34, 56, DateTimeKind.Unspecified);

        Assert.Equal(BewerbungssperreRules.PickedDateToUtc(noon.Date), BewerbungssperreRules.PickedDateToUtc(noon));
    }
}
