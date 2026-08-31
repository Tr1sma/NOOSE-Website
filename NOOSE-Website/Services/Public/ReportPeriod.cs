using System.Globalization;

namespace NOOSE_Website.Services.Public;

/// <summary>The period of a public situation report, which is also its public address.</summary>
/// <remarks>
/// One rule in one place, like PublicExpiry and BountyShares: the hub, the article route and the panel all read it
/// from here. Parsing is strict and answers a value it does not understand with false rather than a guess — the route
/// value comes from an anonymous URL, and Blazor answers an unparsable route value with HTTP 500.
/// </remarks>
public static class ReportPeriod
{
    /// <summary>The address form: 2026-08.</summary>
    public static string Format(int year, int month) => $"{year:D4}-{month:D2}";

    /// <summary>The reading form: August 2026, in the app's fixed de-DE culture.</summary>
    public static string Label(int year, int month)
        => month is >= 1 and <= 12
            ? $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}"
            : Format(year, month);

    public static bool TryParse(string? value, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (value is null || value.Length != 7 || value[4] != '-')
        {
            return false;
        }
        // NumberStyles.None rejects a sign, a space and a thousands separator, all of which int.TryParse would take
        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m))
        {
            return false;
        }
        if (y < 1 || y > 9999 || m < 1 || m > 12)
        {
            return false;
        }
        year = y;
        month = m;
        return true;
    }
}
