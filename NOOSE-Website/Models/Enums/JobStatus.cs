namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status einer Aufgabe/To-Do – Phase 6. Steuert Chip-Anzeige und Listenfilter. Default ist
/// <see cref="Offen"/>. „Erledigt" und „Abgebrochen" gelten als abgeschlossen (setzen den Erledigt-Zeitpunkt).
/// </summary>
public enum JobStatus
{
    /// <summary>Angelegt, aber noch nicht in Bearbeitung.</summary>
    Open = 0,
    /// <summary>Aktiv in Bearbeitung.</summary>
    InProcessing = 1,
    /// <summary>Erfolgreich erledigt.</summary>
    Done = 2,
    /// <summary>Abgebrochen/verworfen (nicht ausgeführt).</summary>
    Aborted = 3,
}

/// <summary>Anzeigetexte für den Aufgaben-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class JobStatusDisplay
{
    public static string Name(JobStatus status) => status switch
    {
        JobStatus.Open => "Offen",
        JobStatus.InProcessing => "In Bearbeitung",
        JobStatus.Done => "Erledigt",
        JobStatus.Aborted => "Abgebrochen",
        _ => "—",
    };

    /// <summary>Status, die eine noch nicht abgeschlossene Aufgabe kennzeichnen – für Zähler/Filter/Überfällig.</summary>
    public static bool IsOpen(JobStatus status)
        => status is JobStatus.Open or JobStatus.InProcessing;

    /// <summary>Status, bei denen der Erledigt-Zeitpunkt gesetzt wird.</summary>
    public static bool IsCompleted(JobStatus status)
        => status is JobStatus.Done or JobStatus.Aborted;

    public static readonly IReadOnlyList<JobStatus> All = new[]
    {
        JobStatus.Open,
        JobStatus.InProcessing,
        JobStatus.Done,
        JobStatus.Aborted,
    };
}
