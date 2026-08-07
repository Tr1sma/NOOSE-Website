using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Evidence;

/// <summary>A catalog item held in the evidence room; on-hand balance is computed from the ledger.</summary>
[Table("Asservate")]
public class EvidenceItem : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Bezeichnung")]
    public string Name { get; set; } = string.Empty;

    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Grouping label from the admin-maintained suggestion catalog.</summary>
    [Column("Kategorie")]
    public string? Category { get; set; }

    /// <summary>Server-assigned image file name; null when no picture uploaded.</summary>
    [Column("BildDatei")]
    public string? ImageFileName { get; set; }

    [Column("BildTyp")]
    public string? ImageContentType { get; set; }

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
