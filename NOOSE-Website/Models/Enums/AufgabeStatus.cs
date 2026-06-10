namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status einer Aufgabe/To-Do – Phase 6. Steuert Chip-Anzeige und Listenfilter. Default ist
/// <see cref="Offen"/>. „Erledigt" und „Abgebrochen" gelten als abgeschlossen (setzen den Erledigt-Zeitpunkt).
/// </summary>
public enum AufgabeStatus
{
    /// <summary>Angelegt, aber noch nicht in Bearbeitung.</summary>
    Offen = 0,
    /// <summary>Aktiv in Bearbeitung.</summary>
    InBearbeitung = 1,
    /// <summary>Erfolgreich erledigt.</summary>
    Erledigt = 2,
    /// <summary>Abgebrochen/verworfen (nicht ausgeführt).</summary>
    Abgebrochen = 3,
}

/// <summary>Anzeigetexte für den Aufgaben-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class AufgabeStatusAnzeige
{
    public static string Name(AufgabeStatus status) => status switch
    {
        AufgabeStatus.Offen => "Offen",
        AufgabeStatus.InBearbeitung => "In Bearbeitung",
        AufgabeStatus.Erledigt => "Erledigt",
        AufgabeStatus.Abgebrochen => "Abgebrochen",
        _ => "—",
    };

    /// <summary>Status, die eine noch nicht abgeschlossene Aufgabe kennzeichnen – für Zähler/Filter/Überfällig.</summary>
    public static bool IstOffen(AufgabeStatus status)
        => status is AufgabeStatus.Offen or AufgabeStatus.InBearbeitung;

    /// <summary>Status, bei denen der Erledigt-Zeitpunkt gesetzt wird.</summary>
    public static bool IstAbgeschlossen(AufgabeStatus status)
        => status is AufgabeStatus.Erledigt or AufgabeStatus.Abgebrochen;

    public static readonly IReadOnlyList<AufgabeStatus> Alle = new[]
    {
        AufgabeStatus.Offen,
        AufgabeStatus.InBearbeitung,
        AufgabeStatus.Erledigt,
        AufgabeStatus.Abgebrochen,
    };
}
