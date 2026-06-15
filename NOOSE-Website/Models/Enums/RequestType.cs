namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Art eines Antrags im Posteingang-Workflow (Phase 5). Aktuell nur <see cref="Hochstufung"/> – die
/// Einstufung „Gesichert staatsgefährdend" für Agenten unterhalb Senior Special Agent läuft über einen
/// Antrag. Der Enum ist bewusst erweiterbar, falls künftig weitere Antragsarten über die generische
/// <c>Antrag</c>-Entität vereinheitlicht werden sollen.
/// </summary>
public enum RequestType
{
    /// <summary>Hochstufung einer Akte auf „Gesichert staatsgefährdend" (Entscheidung: Senior Special Agent+).</summary>
    Upgrade = 0,
}

/// <summary>Anzeigetexte für die Antragsart (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class RequestTypeDisplay
{
    public static string Name(RequestType type) => type switch
    {
        RequestType.Upgrade => "Hochstufung",
        _ => "—",
    };
}
