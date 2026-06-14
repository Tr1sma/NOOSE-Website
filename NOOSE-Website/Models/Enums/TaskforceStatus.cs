namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Genehmigungs-/Lebenszyklus-Status einer Taskforce – Phase 5c. Eine neu angelegte Taskforce ist zunächst
/// <see cref="Beantragt"/>; die Führung genehmigt, lehnt ab oder löst sie auf. Steuert die Anzeige (Chip) auf
/// Karte und Detailseite sowie den Filter auf der Listenseite.
/// </summary>
public enum TaskforceStatus
{
    /// <summary>Beantragt – wartet auf Genehmigung durch die Führung.</summary>
    Requested = 0,
    /// <summary>Genehmigt – aktive Taskforce.</summary>
    Approved = 1,
    /// <summary>Abgelehnt – Antrag von der Führung abgelehnt.</summary>
    Rejected = 2,
    /// <summary>Aufgelöst – ehemals aktive Taskforce, regulär beendet.</summary>
    Resolved = 3,
}

/// <summary>Anzeigetexte für den Taskforce-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class TaskforceStatusDisplay
{
    public static string Name(TaskforceStatus status) => status switch
    {
        TaskforceStatus.Requested => "Beantragt",
        TaskforceStatus.Approved => "Genehmigt",
        TaskforceStatus.Rejected => "Abgelehnt",
        TaskforceStatus.Resolved => "Aufgelöst",
        _ => "—",
    };

    public static readonly IReadOnlyList<TaskforceStatus> All = new[]
    {
        TaskforceStatus.Requested,
        TaskforceStatus.Approved,
        TaskforceStatus.Rejected,
        TaskforceStatus.Resolved,
    };
}
