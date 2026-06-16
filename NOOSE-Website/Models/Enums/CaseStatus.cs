namespace NOOSE_Website.Models.Enums;

/// <summary>Case lifecycle status.</summary>
public enum CaseStatus
{
    /// <summary>Opened, not yet active.</summary>
    Open = 0,
    /// <summary>Actively being worked.</summary>
    InProcessing = 1,
    /// <summary>Dormant, awaiting input.</summary>
    Dormant = 2,
    /// <summary>Closed successfully.</summary>
    Completed = 3,
    /// <summary>Filed away, inactive.</summary>
    Archived = 4,
}

/// <summary>Display labels.</summary>
public static class CaseStatusDisplay
{
    public static string Name(CaseStatus status) => status switch
    {
        CaseStatus.Open => "Offen",
        CaseStatus.InProcessing => "In Bearbeitung",
        CaseStatus.Dormant => "Ruht",
        CaseStatus.Completed => "Abgeschlossen",
        CaseStatus.Archived => "Archiviert",
        _ => "—",
    };

    /// <summary>Not yet finished.</summary>
    public static bool IsOpen(CaseStatus status)
        => status is CaseStatus.Open or CaseStatus.InProcessing or CaseStatus.Dormant;

    /// <summary>Completion timestamp applies.</summary>
    public static bool IsCompleted(CaseStatus status)
        => status is CaseStatus.Completed or CaseStatus.Archived;

    public static readonly IReadOnlyList<CaseStatus> All = new[]
    {
        CaseStatus.Open,
        CaseStatus.InProcessing,
        CaseStatus.Dormant,
        CaseStatus.Completed,
        CaseStatus.Archived,
    };
}
