namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Rolle eines der Taskforce zugeteilten Agents – Phase 5c. <see cref="Mitglied"/> ist ein einfaches
/// Einsatzmitglied; die drei Lead-Rollen (<see cref="Chefermittler"/>, <see cref="CidLead"/>,
/// <see cref="TruLead"/>) markieren die Leitung. „Leitung" = jede Rolle ungleich <see cref="Mitglied"/>:
/// diese Agents dürfen – wie die Führung – Mitglieder der Taskforce verwalten (Pendant zum
/// Ermittlungsleiter-Flag der übrigen Akten). Rollen vergibt ausschließlich die Führung.
/// </summary>
public enum TaskforceRolle
{
    /// <summary>Einfaches Einsatzmitglied (keine Leitung).</summary>
    Mitglied = 0,
    /// <summary>Chefermittler – ermittlungsleitende Verantwortung.</summary>
    Chefermittler = 1,
    /// <summary>CID-Lead – operative Leitung (Criminal Investigation Division).</summary>
    CidLead = 2,
    /// <summary>TRU-Lead – taktisch-operative Leitung (Tactical Response Unit).</summary>
    TruLead = 3,
}

/// <summary>Anzeigetexte für die Taskforce-Rolle (UI-frei).</summary>
public static class TaskforceRolleAnzeige
{
    public static string Name(TaskforceRolle rolle) => rolle switch
    {
        TaskforceRolle.Mitglied => "Mitglied",
        TaskforceRolle.Chefermittler => "Chefermittler",
        TaskforceRolle.CidLead => "CID-Lead",
        TaskforceRolle.TruLead => "TRU-Lead",
        _ => "—",
    };

    /// <summary>Eine Rolle ungleich <see cref="TaskforceRolle.Mitglied"/> zählt als Leitung.</summary>
    public static bool IstLeitung(TaskforceRolle rolle) => rolle != TaskforceRolle.Mitglied;

    public static readonly IReadOnlyList<TaskforceRolle> Alle = new[]
    {
        TaskforceRolle.Mitglied,
        TaskforceRolle.Chefermittler,
        TaskforceRolle.CidLead,
        TaskforceRolle.TruLead,
    };
}
