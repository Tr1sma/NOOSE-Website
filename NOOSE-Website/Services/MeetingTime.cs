using System.Globalization;

namespace NOOSE_Website.Services;

/// <summary>Meeting time helpers; the zone is resolved once so reads and writes always agree.</summary>
public static class MeetingTime
{
    private static readonly TimeZoneInfo Zone = Resolve();

    /// <summary>The pinned zone, for callers doing their own arithmetic.</summary>
    public static TimeZoneInfo ZoneRef => Zone;

    // pinned rather than TimeZoneInfo.Local: a missing TZ must not shift a permanent "fehlend" row
    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    /// <summary>Calendar day a UTC instant falls on; absences are whole-day.</summary>
    public static DateOnly Day(DateTime utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

    /// <summary>Local wall-clock time of a UTC instant.</summary>
    public static DateTime Local(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>German wall-clock text for notification titles.</summary>
    public static string Text(DateTime utc)
        => Local(utc).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Local wall-clock input to UTC; the pinned zone, matching Day and Text.</summary>
    public static DateTime ToUtc(DateTime local)
    {
        var value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // a spring-forward gap has no such wall-clock time; nudge past it instead of throwing
        if (Zone.IsInvalidTime(value))
        {
            value = value.AddHours(1);
        }
        return TimeZoneInfo.ConvertTimeToUtc(value, Zone);
    }
}
