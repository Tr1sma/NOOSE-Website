namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status einer Operation/eines Einsatzberichts – Phase 5b. Steuert die Anzeige (Chip) auf
/// Karte und Detailseite sowie den Filter auf der Listenseite. Default ist <see cref="Geplant"/>.
/// </summary>
public enum OperationStatus
{
    /// <summary>Geplant, noch nicht begonnen.</summary>
    Geplant = 0,
    /// <summary>Laufend (im Einsatz).</summary>
    Laufend = 1,
    /// <summary>Abgeschlossen (regulär beendet).</summary>
    Abgeschlossen = 2,
    /// <summary>Abgebrochen (vorzeitig beendet).</summary>
    Abgebrochen = 3,
}

/// <summary>Anzeigetexte für den Operations-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class OperationStatusAnzeige
{
    public static string Name(OperationStatus status) => status switch
    {
        OperationStatus.Geplant => "Geplant",
        OperationStatus.Laufend => "Laufend",
        OperationStatus.Abgeschlossen => "Abgeschlossen",
        OperationStatus.Abgebrochen => "Abgebrochen",
        _ => "—",
    };

    public static readonly IReadOnlyList<OperationStatus> Alle = new[]
    {
        OperationStatus.Geplant,
        OperationStatus.Laufend,
        OperationStatus.Abgeschlossen,
        OperationStatus.Abgebrochen,
    };
}
