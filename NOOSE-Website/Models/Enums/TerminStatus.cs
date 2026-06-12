namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status eines Termins – Phase 8 (Block C). Default <see cref="Geplant"/>; abgesagte/verschobene
/// Termine werden im Kalender gedämpft (durchgestrichen) dargestellt.
/// </summary>
public enum TerminStatus
{
    /// <summary>Geplant, steht noch an.</summary>
    Geplant = 0,
    /// <summary>Wahrgenommen/stattgefunden.</summary>
    Wahrgenommen = 1,
    /// <summary>Abgesagt (findet nicht statt).</summary>
    Abgesagt = 2,
    /// <summary>Verschoben (neuer Termin folgt separat).</summary>
    Verschoben = 3,
}

/// <summary>Anzeigetexte für den Termin-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class TerminStatusAnzeige
{
    public static string Name(TerminStatus status) => status switch
    {
        TerminStatus.Geplant => "Geplant",
        TerminStatus.Wahrgenommen => "Wahrgenommen",
        TerminStatus.Abgesagt => "Abgesagt",
        TerminStatus.Verschoben => "Verschoben",
        _ => "—",
    };

    /// <summary>Abgesagt oder verschoben – im Kalender gedämpft darstellen.</summary>
    public static bool IstHinfaellig(TerminStatus status) => status is TerminStatus.Abgesagt or TerminStatus.Verschoben;

    public static readonly IReadOnlyList<TerminStatus> Alle = new[]
    {
        TerminStatus.Geplant,
        TerminStatus.Wahrgenommen,
        TerminStatus.Abgesagt,
        TerminStatus.Verschoben,
    };
}
