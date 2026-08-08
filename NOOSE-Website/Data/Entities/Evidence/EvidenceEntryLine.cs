using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Evidence;

/// <summary>One item + quantity within an evidence entry.</summary>
[Table("AsservatPositionen")]
public class EvidenceEntryLine : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("EintragId")]
    public string EntryId { get; set; } = string.Empty;
    public EvidenceEntry? Entry { get; set; }

    [Column("AsservatId")]
    public string ItemId { get; set; } = string.Empty;
    public EvidenceItem? Item { get; set; }

    [Column("Menge")]
    public int Quantity { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
