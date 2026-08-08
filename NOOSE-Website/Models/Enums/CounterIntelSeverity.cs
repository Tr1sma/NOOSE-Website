namespace NOOSE_Website.Models.Enums;

/// <summary>How loud a counter-intelligence rule shouts when it triggers.</summary>
public enum CounterIntelSeverity
{
    Info = 0,
    Warning = 1,
    High = 2,
    Critical = 3,
}

/// <summary>Display labels.</summary>
public static class CounterIntelSeverityDisplay
{
    public static string Name(CounterIntelSeverity severity) => severity switch
    {
        CounterIntelSeverity.Info => "Hinweis",
        CounterIntelSeverity.Warning => "Auffällig",
        CounterIntelSeverity.High => "Hoch",
        CounterIntelSeverity.Critical => "Kritisch",
        _ => severity.ToString(),
    };

    /// <summary>All levels, mildest first.</summary>
    public static readonly IReadOnlyList<CounterIntelSeverity> All =
    [
        CounterIntelSeverity.Info,
        CounterIntelSeverity.Warning,
        CounterIntelSeverity.High,
        CounterIntelSeverity.Critical,
    ];
}
