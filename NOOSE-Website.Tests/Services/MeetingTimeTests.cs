using System.Globalization;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

public class MeetingTimeTests
{
    // A fixed, known UTC instant (winter -> Europe/Berlin is UTC+1).
    private static readonly DateTime WinterUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    // A fixed, known UTC instant (summer -> Europe/Berlin is UTC+2 due to DST).
    private static readonly DateTime SummerUtc = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    // Recompute the "correct" local time straight off the pinned zone so the test
    // does not depend on whatever the host's own local zone happens to be.
    private static DateTime ExpectedLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), MeetingTime.ZoneRef);

    // ---- ZoneRef ---------------------------------------------------------

    [Fact]
    public void ZoneRef_IsNeverNull()
    {
        Assert.NotNull(MeetingTime.ZoneRef);
    }

    [Fact]
    public void ZoneRef_ResolvesToBerlin_OrLocalFallback()
    {
        var zone = MeetingTime.ZoneRef;
        // Either the pinned IANA zone (tz data present) or the documented fallback.
        Assert.True(zone.Id == "Europe/Berlin" || zone.Equals(TimeZoneInfo.Local));
    }

    [Fact]
    public void ZoneRef_IsStableAcrossReads()
    {
        // Resolved once into a static readonly field -> same instance every time.
        Assert.Same(MeetingTime.ZoneRef, MeetingTime.ZoneRef);
    }

    // ---- Local -----------------------------------------------------------

    [Fact]
    public void Local_WinterInstant_MatchesZoneConversion()
    {
        Assert.Equal(ExpectedLocal(WinterUtc), MeetingTime.Local(WinterUtc));
    }

    [Fact]
    public void Local_SummerInstant_MatchesZoneConversion()
    {
        Assert.Equal(ExpectedLocal(SummerUtc), MeetingTime.Local(SummerUtc));
    }

    [Fact]
    public void Local_WinterInstant_IsUtcPlusOne_WhenBerlin()
    {
        if (MeetingTime.ZoneRef.Id != "Europe/Berlin") return;
        Assert.Equal(new DateTime(2026, 1, 15, 13, 0, 0), MeetingTime.Local(WinterUtc));
    }

    [Fact]
    public void Local_SummerInstant_IsUtcPlusTwo_WhenBerlin()
    {
        if (MeetingTime.ZoneRef.Id != "Europe/Berlin") return;
        Assert.Equal(new DateTime(2026, 7, 15, 14, 0, 0), MeetingTime.Local(SummerUtc));
    }

    [Fact]
    public void Local_IgnoresInputKind_TreatsValueAsUtc()
    {
        // Method force-specifies Utc kind, so the incoming Kind must not change the result.
        var asUtc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var asLocal = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);
        var asUnspecified = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);

        var fromUtc = MeetingTime.Local(asUtc);
        Assert.Equal(fromUtc, MeetingTime.Local(asLocal));
        Assert.Equal(fromUtc, MeetingTime.Local(asUnspecified));
    }

    // ---- Day -------------------------------------------------------------

    [Fact]
    public void Day_WinterInstant_MatchesLocalCalendarDay()
    {
        Assert.Equal(DateOnly.FromDateTime(ExpectedLocal(WinterUtc)), MeetingTime.Day(WinterUtc));
    }

    [Fact]
    public void Day_SummerInstant_MatchesLocalCalendarDay()
    {
        Assert.Equal(DateOnly.FromDateTime(ExpectedLocal(SummerUtc)), MeetingTime.Day(SummerUtc));
    }

    [Fact]
    public void Day_LateEveningUtc_RollsIntoNextLocalDay()
    {
        // 23:30 UTC + a positive Berlin offset lands on the following calendar day.
        var lateUtc = new DateTime(2026, 1, 15, 23, 30, 0, DateTimeKind.Utc);
        var expected = DateOnly.FromDateTime(ExpectedLocal(lateUtc));

        Assert.Equal(expected, MeetingTime.Day(lateUtc));
        if (MeetingTime.ZoneRef.Id == "Europe/Berlin")
        {
            Assert.Equal(new DateOnly(2026, 1, 16), MeetingTime.Day(lateUtc));
        }
    }

    [Fact]
    public void Day_IgnoresInputKind()
    {
        var asUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var asUnspecified = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(MeetingTime.Day(asUtc), MeetingTime.Day(asUnspecified));
    }

    // ---- Text ------------------------------------------------------------

    [Fact]
    public void Text_MatchesLocalFormattedWithGermanCulture()
    {
        var expected = MeetingTime.Local(WinterUtc).ToString("dd.MM.yyyy HH:mm", DeDe);
        Assert.Equal(expected, MeetingTime.Text(WinterUtc));
    }

    [Fact]
    public void Text_WinterInstant_HasExpectedString_WhenBerlin()
    {
        if (MeetingTime.ZoneRef.Id != "Europe/Berlin") return;
        Assert.Equal("15.01.2026 13:00", MeetingTime.Text(WinterUtc));
    }

    [Fact]
    public void Text_SummerInstant_HasExpectedString_WhenBerlin()
    {
        if (MeetingTime.ZoneRef.Id != "Europe/Berlin") return;
        Assert.Equal("15.07.2026 14:00", MeetingTime.Text(SummerUtc));
    }

    [Fact]
    public void Text_UsesGermanCulture_RegardlessOfAmbientCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var expected = MeetingTime.Local(SummerUtc).ToString("dd.MM.yyyy HH:mm", DeDe);
            Assert.Equal(expected, MeetingTime.Text(SummerUtc));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Text_UsesTwentyFourHourClock()
    {
        // 12:00 UTC in summer -> 14:00 local; a 24h format must show "14", never "02".
        var text = MeetingTime.Text(SummerUtc);
        Assert.Matches(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$", text);
        Assert.DoesNotContain("AM", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PM", text, StringComparison.OrdinalIgnoreCase);
    }

    // ---- ToUtc: round-trips ---------------------------------------------

    [Fact]
    public void ToUtc_RoundTripsLocal_Winter()
    {
        var local = MeetingTime.Local(WinterUtc);
        Assert.Equal(WinterUtc, MeetingTime.ToUtc(local));
    }

    [Fact]
    public void ToUtc_RoundTripsLocal_Summer()
    {
        var local = MeetingTime.Local(SummerUtc);
        Assert.Equal(SummerUtc, MeetingTime.ToUtc(local));
    }

    [Fact]
    public void ToUtc_ReturnsUtcKind()
    {
        var result = MeetingTime.ToUtc(new DateTime(2026, 6, 1, 10, 0, 0));
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ToUtc_IgnoresInputKind_TreatsValueAsWallClock()
    {
        var wall = new DateTime(2026, 6, 1, 10, 0, 0);
        var asUtcKind = DateTime.SpecifyKind(wall, DateTimeKind.Utc);
        var asLocalKind = DateTime.SpecifyKind(wall, DateTimeKind.Local);
        var asUnspecified = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);

        var baseline = MeetingTime.ToUtc(asUnspecified);
        Assert.Equal(baseline, MeetingTime.ToUtc(asUtcKind));
        Assert.Equal(baseline, MeetingTime.ToUtc(asLocalKind));
    }

    // ---- ToUtc: spring-forward gap --------------------------------------

    [Fact]
    public void ToUtc_SpringForwardInvalidTime_DoesNotThrow()
    {
        // 2026-03-29 02:30 is inside the Europe/Berlin spring-forward gap.
        var invalid = new DateTime(2026, 3, 29, 2, 30, 0);
        var ex = Record.Exception(() => MeetingTime.ToUtc(invalid));
        Assert.Null(ex);
    }

    [Fact]
    public void ToUtc_SpringForwardInvalidTime_NudgesForwardOneHour()
    {
        var invalid = DateTime.SpecifyKind(new DateTime(2026, 3, 29, 2, 30, 0), DateTimeKind.Unspecified);

        // Only assert the nudge when the pinned zone actually treats this as invalid
        // (a non-DST fallback zone would not), keeping the test host-independent.
        if (!MeetingTime.ZoneRef.IsInvalidTime(invalid)) return;

        var expected = TimeZoneInfo.ConvertTimeToUtc(invalid.AddHours(1), MeetingTime.ZoneRef);
        Assert.Equal(expected, MeetingTime.ToUtc(invalid));
    }

    [Fact]
    public void ToUtc_SpringForwardInvalidTime_EqualsUtcOfNudgedWallClock_WhenBerlin()
    {
        if (MeetingTime.ZoneRef.Id != "Europe/Berlin") return;

        // 02:30 -> nudged to 03:30 CEST (UTC+2) -> 01:30 UTC.
        var invalid = new DateTime(2026, 3, 29, 2, 30, 0);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), MeetingTime.ToUtc(invalid));
    }

    [Fact]
    public void ToUtc_ValidTimeJustBeforeGap_IsNotNudged()
    {
        // 01:30 (before the 02:00 gap) is a valid wall-clock time and must convert directly.
        var valid = DateTime.SpecifyKind(new DateTime(2026, 3, 29, 1, 30, 0), DateTimeKind.Unspecified);
        var expected = TimeZoneInfo.ConvertTimeToUtc(valid, MeetingTime.ZoneRef);
        Assert.Equal(expected, MeetingTime.ToUtc(valid));
    }
}
