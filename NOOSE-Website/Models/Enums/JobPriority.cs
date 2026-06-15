namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Priorität einer Aufgabe/To-Do – Phase 6. Steuert Sortierung und Chip-Farbe. Default ist <see cref="Normal"/>.
/// </summary>
public enum JobPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

/// <summary>Anzeigetexte für die Aufgaben-Priorität (UI-frei, ohne MudBlazor-Abhängigkeit; Farbe im Chip).</summary>
public static class JobPriorityDisplay
{
    public static string Name(JobPriority priority) => priority switch
    {
        JobPriority.Low => "Niedrig",
        JobPriority.Normal => "Normal",
        JobPriority.High => "Hoch",
        _ => "—",
    };

    public static readonly IReadOnlyList<JobPriority> All = new[]
    {
        JobPriority.Low,
        JobPriority.Normal,
        JobPriority.High,
    };
}
