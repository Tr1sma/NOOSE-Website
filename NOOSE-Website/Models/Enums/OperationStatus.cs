namespace NOOSE_Website.Models.Enums;

/// <summary>Operation lifecycle status.</summary>
public enum OperationStatus
{
    /// <summary>Planned, not started.</summary>
    Planned = 0,
    /// <summary>Currently running.</summary>
    Running = 1,
    /// <summary>Successfully completed.</summary>
    Completed = 2,
    /// <summary>Terminated early.</summary>
    Aborted = 3,
}

/// <summary>Display labels.</summary>
public static class OperationStatusDisplay
{
    public static string Name(OperationStatus status) => status switch
    {
        OperationStatus.Planned => "Geplant",
        OperationStatus.Running => "Laufend",
        OperationStatus.Completed => "Abgeschlossen",
        OperationStatus.Aborted => "Abgebrochen",
        _ => "—",
    };

    public static readonly IReadOnlyList<OperationStatus> All = new[]
    {
        OperationStatus.Planned,
        OperationStatus.Running,
        OperationStatus.Completed,
        OperationStatus.Aborted,
    };
}
