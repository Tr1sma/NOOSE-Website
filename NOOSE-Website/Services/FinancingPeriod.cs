namespace NOOSE_Website.Services;

/// <summary>Calendar-month arithmetic of the funding budget. One place, so budget reads and approvals always agree on which month is running.</summary>
public static class FinancingPeriod
{
    /// <summary>The running budget month.</summary>
    /// <remarks>Local calendar month on purpose: the server runs in Europe/Berlin, which is the month an agent sees.</remarks>
    public static (int Year, int Month) Current()
    {
        var now = DateTime.Now;
        return (now.Year, now.Month);
    }

    public static (int Year, int Month) Next(int year, int month) => month == 12 ? (year + 1, 1) : (year, month + 1);

    public static (int Year, int Month) Previous(int year, int month) => month == 1 ? (year - 1, 12) : (year, month - 1);

    /// <summary>True if the first period lies strictly before the second.</summary>
    public static bool IsBefore(int year, int month, int otherYear, int otherMonth)
        => year < otherYear || (year == otherYear && month < otherMonth);

    /// <summary>German month label, e.g. "August 2026".</summary>
    public static string Label(int year, int month)
        => new DateTime(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
}
