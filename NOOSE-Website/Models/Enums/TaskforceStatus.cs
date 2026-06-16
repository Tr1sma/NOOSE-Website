namespace NOOSE_Website.Models.Enums;

/// <summary>Taskforce approval status.</summary>
public enum TaskforceStatus
{
    /// <summary>Awaiting leadership approval.</summary>
    Requested = 0,
    /// <summary>Active, approved.</summary>
    Approved = 1,
    /// <summary>Denied by leadership.</summary>
    Rejected = 2,
    /// <summary>Dissolved, concluded.</summary>
    Resolved = 3,
}

/// <summary>Display labels.</summary>
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
