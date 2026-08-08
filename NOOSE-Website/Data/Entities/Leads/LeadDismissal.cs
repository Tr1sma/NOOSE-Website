using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Leads;

/// <summary>A dismissed investigation lead. Soft-deletable so "Rückgängig" simply restores it.</summary>
[Table("HinweisIgnorierungen")]
public class LeadDismissal : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Deterministic key of the dismissed lead (ids ordinally sorted).</summary>
    [Column("HinweisSchluessel")]
    public string LeadKey { get; set; } = string.Empty;

    [Column("Art")]
    public LeadKind Kind { get; set; }

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
