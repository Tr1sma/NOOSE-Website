using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Attendance state of one agent at one meeting.</summary>
public enum MeetingAttendanceStatus
{
    /// <summary>Not decided yet; only before attendance is closed.</summary>
    Open = 0,
    /// <summary>Marked present by leadership.</summary>
    Present = 1,
    /// <summary>Excused by a date range or a per-meeting sign-off.</summary>
    SignedOff = 2,
    /// <summary>Neither present nor excused.</summary>
    Missing = 3,
}

/// <summary>Display labels and icons.</summary>
public static class MeetingAttendanceStatusDisplay
{
    public static string Name(MeetingAttendanceStatus status) => status switch
    {
        MeetingAttendanceStatus.Open => "Offen",
        MeetingAttendanceStatus.Present => "Anwesend",
        MeetingAttendanceStatus.SignedOff => "Abgemeldet",
        MeetingAttendanceStatus.Missing => "Fehlend",
        _ => "—",
    };

    public static string Icon(MeetingAttendanceStatus status) => status switch
    {
        MeetingAttendanceStatus.Open => Icons.Material.Filled.HelpOutline,
        MeetingAttendanceStatus.Present => Icons.Material.Filled.CheckCircle,
        MeetingAttendanceStatus.SignedOff => Icons.Material.Filled.EventBusy,
        MeetingAttendanceStatus.Missing => Icons.Material.Filled.Cancel,
        _ => Icons.Material.Filled.HelpOutline,
    };

    /// <summary>Palette colour; missing is the only state that reads as a problem.</summary>
    public static Color Colour(MeetingAttendanceStatus status) => status switch
    {
        MeetingAttendanceStatus.Open => Color.Default,
        MeetingAttendanceStatus.Present => Color.Success,
        MeetingAttendanceStatus.SignedOff => Color.Info,
        MeetingAttendanceStatus.Missing => Color.Error,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<MeetingAttendanceStatus> All = new[]
    {
        MeetingAttendanceStatus.Open,
        MeetingAttendanceStatus.Present,
        MeetingAttendanceStatus.SignedOff,
        MeetingAttendanceStatus.Missing,
    };
}
