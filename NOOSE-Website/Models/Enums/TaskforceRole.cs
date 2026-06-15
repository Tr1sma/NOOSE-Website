namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Rolle eines der Taskforce zugeteilten Agents – Phase 5c. <see cref="Mitglied"/> ist ein einfaches
/// Einsatzmitglied; die drei Lead-Rollen (<see cref="Chefermittler"/>, <see cref="CidLead"/>,
/// <see cref="TruLead"/>) markieren die Leitung. „Leitung" = jede Rolle ungleich <see cref="Mitglied"/>:
/// diese Agents dürfen – wie die Führung – Mitglieder der Taskforce verwalten (Pendant zum
/// Ermittlungsleiter-Flag der übrigen Akten). Rollen vergibt ausschließlich die Führung.
/// </summary>
public enum TaskforceRole
{
    /// <summary>Einfaches Einsatzmitglied (keine Leitung).</summary>
    Member = 0,
    /// <summary>Chefermittler – ermittlungsleitende Verantwortung.</summary>
    LeadInvestigator = 1,
    /// <summary>CID-Lead – operative Leitung (Criminal Investigation Division).</summary>
    CidLead = 2,
    /// <summary>TRU-Lead – taktisch-operative Leitung (Tactical Response Unit).</summary>
    TruLead = 3,
}

/// <summary>Anzeigetexte für die Taskforce-Rolle (UI-frei).</summary>
public static class TaskforceRoleDisplay
{
    public static string Name(TaskforceRole role) => role switch
    {
        TaskforceRole.Member => "Mitglied",
        TaskforceRole.LeadInvestigator => "Chefermittler",
        TaskforceRole.CidLead => "CID-Lead",
        TaskforceRole.TruLead => "TRU-Lead",
        _ => "—",
    };

    /// <summary>Eine Rolle ungleich <see cref="TaskforceRolle.Mitglied"/> zählt als Leitung.</summary>
    public static bool IsLead(TaskforceRole role) => role != TaskforceRole.Member;

    public static readonly IReadOnlyList<TaskforceRole> All = new[]
    {
        TaskforceRole.Member,
        TaskforceRole.LeadInvestigator,
        TaskforceRole.CidLead,
        TaskforceRole.TruLead,
    };
}
