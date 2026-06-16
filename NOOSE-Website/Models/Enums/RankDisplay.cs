namespace NOOSE_Website.Models.Enums;

/// <summary>Display labels.</summary>
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

    /// <summary>All ranks ascending.</summary>
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

/// <summary>Display labels.</summary>
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
