namespace NOOSE_Website.Models.Enums;

/// <summary>Where a signed-off attendance state came from.</summary>
public enum MeetingAbsenceOrigin
{
    /// <summary>Not signed off.</summary>
    None = 0,
    /// <summary>Covered by a date-range absence.</summary>
    Absence = 1,
    /// <summary>Signed off for this meeting only.</summary>
    MeetingSignOff = 2,
    /// <summary>Set by leadership by hand.</summary>
    Manual = 3,
}

/// <summary>Display labels.</summary>
public static class MeetingAbsenceOriginDisplay
{
    public static string Name(MeetingAbsenceOrigin origin) => origin switch
    {
        MeetingAbsenceOrigin.None => "—",
        MeetingAbsenceOrigin.Absence => "Abmeldung (Zeitraum)",
        MeetingAbsenceOrigin.MeetingSignOff => "Abmeldung (Besprechung)",
        MeetingAbsenceOrigin.Manual => "Manuell erfasst",
        _ => "—",
    };

    public static readonly IReadOnlyList<MeetingAbsenceOrigin> All = new[]
    {
        MeetingAbsenceOrigin.None,
        MeetingAbsenceOrigin.Absence,
        MeetingAbsenceOrigin.MeetingSignOff,
        MeetingAbsenceOrigin.Manual,
    };
}
