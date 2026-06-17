namespace NOOSE_Website.Models.People;

/// <summary>Form model for creating/editing an observation.</summary>
public class ObservationInput
{
    public DateTime Start { get; set; } = DateTime.UtcNow;

    public DateTime? End { get; set; }

    public string? Location { get; set; }
    public string? Sighting { get; set; }
    public string? Result { get; set; }

    public string? ObservingAgentId { get; set; }

    /// <summary>Linked org type: nameof(Fraktion)/nameof(Personengruppe) or null.</summary>
    public string? OrgType { get; set; }

    public string? OrgId { get; set; }
}
