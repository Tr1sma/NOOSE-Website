namespace NOOSE_Website.Models.Enums;

/// <summary>Anzeigetexte für den NOOSE-Dienstgrad (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class RankDisplay
{
    public static string Name(Rank? rank) => rank switch
    {
        Rank.JuniorAgent => "Junior Agent",
        Rank.SpecialAgent => "Special Agent",
        Rank.SeniorSpecialAgent => "Senior Special Agent",
        Rank.SupervisorySpecialAgent => "Supervisory Special Agent",
        Rank.DeputyDirector => "Deputy Director",
        Rank.Director => "Director",
        _ => "— (kein Rang)",
    };

    /// <summary>Alle Dienstgrade in aufsteigender Reihenfolge (für Auswahl-Listen).</summary>
    public static readonly IReadOnlyList<Rank> All = new[]
    {
        Rank.JuniorAgent,
        Rank.SpecialAgent,
        Rank.SeniorSpecialAgent,
        Rank.SupervisorySpecialAgent,
        Rank.DeputyDirector,
        Rank.Director,
    };
}

/// <summary>Anzeigetexte für den Account-Status.</summary>
public static class AgentStatusDisplay
{
    public static string Name(AgentStatus status) => status switch
    {
        AgentStatus.Pending => "Ausstehend",
        AgentStatus.Active => "Aktiv",
        AgentStatus.Blocked => "Gesperrt",
        _ => "—",
    };
}
