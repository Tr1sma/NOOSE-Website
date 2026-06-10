namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Genehmigungs-/Lebenszyklus-Status einer Taskforce – Phase 5c. Eine neu angelegte Taskforce ist zunächst
/// <see cref="Beantragt"/>; die Führung genehmigt, lehnt ab oder löst sie auf. Steuert die Anzeige (Chip) auf
/// Karte und Detailseite sowie den Filter auf der Listenseite.
/// </summary>
public enum TaskforceStatus
{
    /// <summary>Beantragt – wartet auf Genehmigung durch die Führung.</summary>
    Beantragt = 0,
    /// <summary>Genehmigt – aktive Taskforce.</summary>
    Genehmigt = 1,
    /// <summary>Abgelehnt – Antrag von der Führung abgelehnt.</summary>
    Abgelehnt = 2,
    /// <summary>Aufgelöst – ehemals aktive Taskforce, regulär beendet.</summary>
    Aufgeloest = 3,
}

/// <summary>Anzeigetexte für den Taskforce-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class TaskforceStatusAnzeige
{
    public static string Name(TaskforceStatus status) => status switch
    {
        TaskforceStatus.Beantragt => "Beantragt",
        TaskforceStatus.Genehmigt => "Genehmigt",
        TaskforceStatus.Abgelehnt => "Abgelehnt",
        TaskforceStatus.Aufgeloest => "Aufgelöst",
        _ => "—",
    };

    public static readonly IReadOnlyList<TaskforceStatus> Alle = new[]
    {
        TaskforceStatus.Beantragt,
        TaskforceStatus.Genehmigt,
        TaskforceStatus.Abgelehnt,
        TaskforceStatus.Aufgeloest,
    };
}
