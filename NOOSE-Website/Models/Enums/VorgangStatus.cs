namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status einer Vorgangs-/Fallakte – Phase 5. Steuert die Anzeige (Chip) auf Karte und
/// Detailseite sowie den Filter auf der Listenseite. Default ist <see cref="Offen"/>. „Ruht" deckt
/// wartende/auf-Wiedervorlage liegende Vorgänge ab; „Archiviert" trennt abgelegte von gerade
/// abgeschlossenen Fällen.
/// </summary>
public enum VorgangStatus
{
    /// <summary>Eröffnet, aber noch nicht in aktiver Bearbeitung.</summary>
    Offen = 0,
    /// <summary>Aktiv in Bearbeitung.</summary>
    InBearbeitung = 1,
    /// <summary>Ruht (z. B. auf Wiedervorlage, wartend auf Zuarbeit).</summary>
    Ruht = 2,
    /// <summary>Abgeschlossen (regulär beendet).</summary>
    Abgeschlossen = 3,
    /// <summary>Archiviert (abgelegt, nicht mehr aktiv).</summary>
    Archiviert = 4,
}

/// <summary>Anzeigetexte für den Vorgangs-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class VorgangStatusAnzeige
{
    public static string Name(VorgangStatus status) => status switch
    {
        VorgangStatus.Offen => "Offen",
        VorgangStatus.InBearbeitung => "In Bearbeitung",
        VorgangStatus.Ruht => "Ruht",
        VorgangStatus.Abgeschlossen => "Abgeschlossen",
        VorgangStatus.Archiviert => "Archiviert",
        _ => "—",
    };

    /// <summary>Status, die einen „offenen" (noch nicht erledigten) Vorgang kennzeichnen – für Zähler/Filter.</summary>
    public static bool IstOffen(VorgangStatus status)
        => status is VorgangStatus.Offen or VorgangStatus.InBearbeitung or VorgangStatus.Ruht;

    /// <summary>Status, bei denen der Abschluss-Zeitpunkt gesetzt wird.</summary>
    public static bool IstAbgeschlossen(VorgangStatus status)
        => status is VorgangStatus.Abgeschlossen or VorgangStatus.Archiviert;

    public static readonly IReadOnlyList<VorgangStatus> Alle = new[]
    {
        VorgangStatus.Offen,
        VorgangStatus.InBearbeitung,
        VorgangStatus.Ruht,
        VorgangStatus.Abgeschlossen,
        VorgangStatus.Archiviert,
    };
}
