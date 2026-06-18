using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>A surveillance/observation entry on a person, kept separate from interrogation docs. No case number, no life-status logic.</summary>
[Table("Observationen")]
public class Observation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Observation window start (RP time, stored UTC). Required.</summary>
    [Column("Beginn")]
    public DateTime Start { get; set; }

    /// <summary>Observation window end (optional, RP time, stored UTC).</summary>
    [Column("Ende")]
    public DateTime? End { get; set; }

    [Column("Ort")]
    public string? Location { get; set; }

    [Column("Beobachtung")]
    public string? Sighting { get; set; }

    [Column("Ergebnis")]
    public string? Result { get; set; }

    /// <summary>Observing agent; may differ from the recording user. Null when none chosen or agent deleted (FK SetNull).</summary>
    [Column("BeobachtenderAgentId")]
    public string? ObservingAgentId { get; set; }
    public Agent? ObservingAgent { get; set; }

    /// <summary>Linked organization type; null when no record is linked.</summary>
    [Column("OrgTyp")]
    public string? OrgType { get; set; }

    /// <summary>Linked organization id (loose link, no FK).</summary>
    public string? OrgId { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
