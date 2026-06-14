namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status einer Operation/eines Einsatzberichts – Phase 5b. Steuert die Anzeige (Chip) auf
/// Karte und Detailseite sowie den Filter auf der Listenseite. Default ist <see cref="Geplant"/>.
/// </summary>
public enum OperationStatus
{
    /// <summary>Geplant, noch nicht begonnen.</summary>
    Planned = 0,
    /// <summary>Laufend (im Einsatz).</summary>
    Running = 1,
    /// <summary>Abgeschlossen (regulär beendet).</summary>
    Completed = 2,
    /// <summary>Abgebrochen (vorzeitig beendet).</summary>
    Aborted = 3,
}

/// <summary>Anzeigetexte für den Operations-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class OperationStatusDisplay
{
    public static string Name(OperationStatus status) => status switch
    {
        OperationStatus.Planned => "Geplant",
        OperationStatus.Running => "Laufend",
        OperationStatus.Completed => "Abgeschlossen",
        OperationStatus.Aborted => "Abgebrochen",
        _ => "—",
    };

    public static readonly IReadOnlyList<OperationStatus> All = new[]
    {
        OperationStatus.Planned,
        OperationStatus.Running,
        OperationStatus.Completed,
        OperationStatus.Aborted,
    };
}
