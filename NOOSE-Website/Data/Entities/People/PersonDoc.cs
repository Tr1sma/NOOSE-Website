using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>An interrogation/measure protocol on a person. The outcome can affect the person's life status.</summary>
[Table("PersonDoks")]
public class PersonDoc : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Measure time (RP time, stored UTC).</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    [Column("Grund")]
    public string? Reason { get; set; }

    /// <summary>Free-text faction fallback when the organization has no record yet; otherwise linked via OrgType/OrgId.</summary>
    [Column("Fraktion")]
    public string? Faction { get; set; }

    /// <summary>Linked organization type; null when no record is linked (then the free-text faction applies).</summary>
    [Column("OrgTyp")]
    public string? OrgType { get; set; }

    /// <summary>Linked organization id (loose link, no FK).</summary>
    public string? OrgId { get; set; }

    [Column("ErhalteneInformationen")]
    public string? ReceivedInformation { get; set; }

    [Column("Wahrheitsserum")]
    public bool TruthSerum { get; set; }

    [Column("Ausgang")]
    public MeasureOutcome Outcome { get; set; }

    /// <summary>Amnesty injection: person survives but loses their memory.</summary>
    [Column("GedaechtnisGeloescht")]
    public bool MemoryDeleted { get; set; }

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
