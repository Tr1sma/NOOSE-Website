using System.Globalization;

namespace NOOSE_Website.Services;

/// <summary>ISO-8601 week arithmetic of the AI token quota. One place, so quota reads and charges always agree on which week is running.</summary>
public static class IsoWeekPeriod
{
    /// <summary>The running quota week.</summary>
    /// <remarks>Local week on purpose: the server runs in Europe/Berlin, which is the week an agent sees.</remarks>
    public static (int Year, int Week) Current() => From(DateTime.Now);

    /// <summary>ISO year and week of a local timestamp; around New Year the ISO year differs from the calendar year.</summary>
    public static (int Year, int Week) From(DateTime local)
    {
        var date = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
    }

    /// <summary>52, or 53 when the year carries a long week; never assume 52.</summary>
    public static int WeeksInYear(int year) => ISOWeek.GetWeeksInYear(year);

    public static (int Year, int Week) Next(int year, int week)
        => week >= ISOWeek.GetWeeksInYear(year) ? (year + 1, 1) : (year, week + 1);

    public static (int Year, int Week) Previous(int year, int week)
        => week <= 1 ? (year - 1, ISOWeek.GetWeeksInYear(year - 1)) : (year, week - 1);

    /// <summary>True if the first period lies strictly before the second.</summary>
    public static bool IsBefore(int year, int week, int otherYear, int otherWeek)
        => year < otherYear || (year == otherYear && week < otherWeek);

    /// <summary>Monday 00:00 local that opens the week; a stored week the year does not have is clamped instead of throwing.</summary>
    public static DateTime Start(int year, int week)
        => ISOWeek.ToDateTime(year, Math.Clamp(week, 1, ISOWeek.GetWeeksInYear(year)), DayOfWeek.Monday);

    /// <summary>Monday 00:00 local the successor opens at — the moment the quota resets.</summary>
    public static DateTime Reset(int year, int week) => Start(year, week).AddDays(7);

    /// <summary>German week label, e.g. "KW 32/2026".</summary>
    public static string Label(int year, int week)
        => string.Create(CultureInfo.InvariantCulture, $"KW {week:00}/{year}");
}
