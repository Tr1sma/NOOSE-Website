namespace NOOSE_Website.Models.Enums;

/// <summary>How much of the absence roster a viewer may read.</summary>
public enum AbsenceViewScope
{
    /// <summary>Only the viewer's own absences.</summary>
    Own = 0,

    /// <summary>Own plus the in-house roster, without reason or acknowledgement.</summary>
    Team = 1,

    /// <summary>Everything including free text; leadership and read-only supervision.</summary>
    All = 2,
}
