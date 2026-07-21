namespace NOOSE_Website.Models.Enums;

/// <summary>Meeting lifecycle status.</summary>
public enum MeetingStatus
{
    /// <summary>Upcoming, planned.</summary>
    Planned = 0,
    /// <summary>Took place, attendance closed.</summary>
    Held = 1,
    /// <summary>Canceled, won't happen.</summary>
    Canceled = 2,
    /// <summary>Postponed, rescheduled.</summary>
    Postponed = 3,
}

/// <summary>Display labels.</summary>
public static class MeetingStatusDisplay
{
    public static string Name(MeetingStatus status) => status switch
    {
        MeetingStatus.Planned => "Geplant",
        MeetingStatus.Held => "Durchgeführt",
        MeetingStatus.Canceled => "Abgesagt",
        MeetingStatus.Postponed => "Verschoben",
        _ => "—",
    };

    /// <summary>Canceled or postponed.</summary>
    public static bool IsObsolete(MeetingStatus status) => status is MeetingStatus.Canceled or MeetingStatus.Postponed;

    public static readonly IReadOnlyList<MeetingStatus> All = new[]
    {
        MeetingStatus.Planned,
        MeetingStatus.Held,
        MeetingStatus.Canceled,
        MeetingStatus.Postponed,
    };
}
