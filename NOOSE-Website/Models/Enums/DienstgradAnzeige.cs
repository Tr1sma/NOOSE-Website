namespace NOOSE_Website.Models.Enums;

/// <summary>Anzeigetexte für den NOOSE-Dienstgrad (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class DienstgradAnzeige
{
    public static string Name(Dienstgrad? dienstgrad) => dienstgrad switch
    {
        Dienstgrad.JuniorAgent => "Junior Agent",
        Dienstgrad.SpecialAgent => "Special Agent",
        Dienstgrad.SeniorSpecialAgent => "Senior Special Agent",
        Dienstgrad.SupervisorySpecialAgent => "Supervisory Special Agent",
        Dienstgrad.DeputyDirector => "Deputy Director",
        Dienstgrad.Director => "Director",
        _ => "— (kein Rang)",
    };

    /// <summary>Alle Dienstgrade in aufsteigender Reihenfolge (für Auswahl-Listen).</summary>
    public static readonly IReadOnlyList<Dienstgrad> Alle = new[]
    {
        Dienstgrad.JuniorAgent,
        Dienstgrad.SpecialAgent,
        Dienstgrad.SeniorSpecialAgent,
        Dienstgrad.SupervisorySpecialAgent,
        Dienstgrad.DeputyDirector,
        Dienstgrad.Director,
    };
}

/// <summary>Anzeigetexte für den Account-Status.</summary>
public static class AgentStatusAnzeige
{
    public static string Name(AgentStatus status) => status switch
    {
        AgentStatus.Ausstehend => "Ausstehend",
        AgentStatus.Aktiv => "Aktiv",
        AgentStatus.Gesperrt => "Gesperrt",
        _ => "—",
    };
}
