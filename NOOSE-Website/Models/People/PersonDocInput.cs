using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>Form model for creating a person doc (interrogation/measure).</summary>
public class PersonDocInput
{
    /// <summary>Measure time; for "shot" this anchors the 20-minute dead window.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    /// <summary>Free-text faction; fallback when no record is linked.</summary>
    public string? Faction { get; set; }

    /// <summary>Linked org type: nameof(Fraktion)/nameof(Personengruppe) or null.</summary>
    public string? OrgType { get; set; }

    public string? OrgId { get; set; }

    /// <summary>Input-only: also add the person as a member of the linked org.</summary>
    public bool AsMember { get; set; }

    public string? ReceivedInformation { get; set; }
    public bool TruthSerum { get; set; }
    public MeasureOutcome Outcome { get; set; } = MeasureOutcome.RunningStill;
}
