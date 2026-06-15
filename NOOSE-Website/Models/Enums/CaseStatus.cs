namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status einer Vorgangs-/Fallakte – Phase 5. Steuert die Anzeige (Chip) auf Karte und
/// Detailseite sowie den Filter auf der Listenseite. Default ist <see cref="Offen"/>. „Ruht" deckt
/// wartende/auf-Wiedervorlage liegende Vorgänge ab; „Archiviert" trennt abgelegte von gerade
/// abgeschlossenen Fällen.
/// </summary>
public enum CaseStatus
{
    /// <summary>Eröffnet, aber noch nicht in aktiver Bearbeitung.</summary>
    Open = 0,
    /// <summary>Aktiv in Bearbeitung.</summary>
    InProcessing = 1,
    /// <summary>Ruht (z. B. auf Wiedervorlage, wartend auf Zuarbeit).</summary>
    Dormant = 2,
    /// <summary>Abgeschlossen (regulär beendet).</summary>
    Completed = 3,
    /// <summary>Archiviert (abgelegt, nicht mehr aktiv).</summary>
    Archived = 4,
}

/// <summary>Anzeigetexte für den Vorgangs-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class CaseStatusDisplay
{
    public static string Name(CaseStatus status) => status switch
    {
        CaseStatus.Open => "Offen",
        CaseStatus.InProcessing => "In Bearbeitung",
        CaseStatus.Dormant => "Ruht",
        CaseStatus.Completed => "Abgeschlossen",
        CaseStatus.Archived => "Archiviert",
        _ => "—",
    };

    /// <summary>Status, die einen „offenen" (noch nicht erledigten) Vorgang kennzeichnen – für Zähler/Filter.</summary>
    public static bool IsOpen(CaseStatus status)
        => status is CaseStatus.Open or CaseStatus.InProcessing or CaseStatus.Dormant;

    /// <summary>Status, bei denen der Abschluss-Zeitpunkt gesetzt wird.</summary>
    public static bool IsCompleted(CaseStatus status)
        => status is CaseStatus.Completed or CaseStatus.Archived;

    public static readonly IReadOnlyList<CaseStatus> All = new[]
    {
        CaseStatus.Open,
        CaseStatus.InProcessing,
        CaseStatus.Dormant,
        CaseStatus.Completed,
        CaseStatus.Archived,
    };
}
