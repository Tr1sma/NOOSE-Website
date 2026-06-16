using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Record freshness level.</summary>
public enum RecencyLevel
{
    /// <summary>Current (green).</summary>
    Fresh = 0,

    /// <summary>Aging (yellow).</summary>
    Warning = 1,

    /// <summary>Outdated (red).</summary>
    Stale = 2,
}

/// <summary>Calculates recency from thresholds.</summary>
public static class RecencyAssessment
{
    /// <summary>Maps age in days to recency level.</summary>
    public static RecencyLevel Level(int warningDays, int staleDays, DateTime referenceDate, DateTime now)
    {
        var alterDays = (now - referenceDate).TotalDays;
        if (alterDays >= staleDays)
        {
            return RecencyLevel.Stale;
        }
        return alterDays >= warningDays ? RecencyLevel.Warning : RecencyLevel.Fresh;
    }
}

/// <summary>Display labels.</summary>
public static class RecencyLevelDisplay
{
    public static string Name(RecencyLevel level) => level switch
    {
        RecencyLevel.Fresh => "Aktuell",
        RecencyLevel.Warning => "Wird älter",
        RecencyLevel.Stale => "Veraltet",
        _ => "Unbekannt",
    };

    public static Color Colour(RecencyLevel level) => level switch
    {
        RecencyLevel.Fresh => Color.Success,
        RecencyLevel.Warning => Color.Warning,
        RecencyLevel.Stale => Color.Error,
        _ => Color.Default,
    };
}
