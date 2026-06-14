namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Geltungsbereich einer Taskforce – Phase 5c. Legt fest, ob die Einsatzgruppe rein NOOSE-intern arbeitet
/// (<see cref="Innerbehoerdlich"/>) oder behördenübergreifend mit Partnern (DoJ/LSPD/LSMD) zusammengesetzt ist
/// (<see cref="Ueberbehoerdlich"/>).
/// </summary>
public enum TaskforceScope
{
    /// <summary>Innerbehördlich – ausschließlich NOOSE.</summary>
    InternalAgency = 0,
    /// <summary>Überbehördlich – behördenübergreifend (mit Partnerbehörden).</summary>
    CrossAgency = 1,
}

/// <summary>Anzeigetexte für den Taskforce-Geltungsbereich (UI-frei).</summary>
public static class TaskforceScopeDisplay
{
    public static string Name(TaskforceScope area) => area switch
    {
        TaskforceScope.InternalAgency => "Innerbehördlich",
        TaskforceScope.CrossAgency => "Überbehördlich",
        _ => "—",
    };

    public static readonly IReadOnlyList<TaskforceScope> All = new[]
    {
        TaskforceScope.InternalAgency,
        TaskforceScope.CrossAgency,
    };
}
