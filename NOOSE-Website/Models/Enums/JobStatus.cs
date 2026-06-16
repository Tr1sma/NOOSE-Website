namespace NOOSE_Website.Models.Enums;

/// <summary>Task lifecycle status.</summary>
public enum JobStatus
{
    /// <summary>Created, not started.</summary>
    Open = 0,
    /// <summary>Actively in progress.</summary>
    InProcessing = 1,
    /// <summary>Successfully completed.</summary>
    Done = 2,
    /// <summary>Abandoned, not executed.</summary>
    Aborted = 3,
}

/// <summary>Display labels.</summary>
public static class JobStatusDisplay
{
    public static string Name(JobStatus status) => status switch
    {
        JobStatus.Open => "Offen",
        JobStatus.InProcessing => "In Bearbeitung",
        JobStatus.Done => "Erledigt",
        JobStatus.Aborted => "Abgebrochen",
        _ => "—",
    };

    /// <summary>Not yet finished.</summary>
    public static bool IsOpen(JobStatus status)
        => status is JobStatus.Open or JobStatus.InProcessing;

    /// <summary>Completion timestamp applies.</summary>
    public static bool IsCompleted(JobStatus status)
        => status is JobStatus.Done or JobStatus.Aborted;

    public static readonly IReadOnlyList<JobStatus> All = new[]
    {
        JobStatus.Open,
        JobStatus.InProcessing,
        JobStatus.Done,
        JobStatus.Aborted,
    };
}
