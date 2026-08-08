using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Severity of the information disclosed during an abduction.</summary>
public enum LeakSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>Display labels and chip colors.</summary>
public static class LeakSeverityDisplay
{
    public static string Name(LeakSeverity severity) => severity switch
    {
        LeakSeverity.None => "Kein Abfluss",
        LeakSeverity.Low => "Gering",
        LeakSeverity.Medium => "Mittel",
        LeakSeverity.High => "Hoch",
        LeakSeverity.Critical => "Kritisch",
        _ => "—",
    };

    public static Color ChipColor(LeakSeverity severity) => severity switch
    {
        LeakSeverity.None => Color.Default,
        LeakSeverity.Low => Color.Info,
        LeakSeverity.Medium => Color.Warning,
        LeakSeverity.High => Color.Error,
        LeakSeverity.Critical => Color.Error,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<LeakSeverity> All = new[]
    {
        LeakSeverity.None,
        LeakSeverity.Low,
        LeakSeverity.Medium,
        LeakSeverity.High,
        LeakSeverity.Critical,
    };
}
