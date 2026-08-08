using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Financing;

/// <summary>One basket line of a funding request; name, price and share are copied from the catalog so later catalog edits never move a filed request.</summary>
[Table("Finanzierungsantragspositionen")]
public class FinancingRequestLine : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("AntragId")]
    public string RequestId { get; set; } = string.Empty;
    public FinancingRequest? Request { get; set; }

    /// <summary>Source position; null once the catalog entry is gone.</summary>
    [Column("PositionId")]
    public string? ItemId { get; set; }
    public FinancingItem? Item { get; set; }

    [Column("Bezeichnung")]
    public string ItemName { get; set; } = string.Empty;

    [Column("Kategorie")]
    public string? Category { get; set; }

    [Column("Einzelpreis")]
    public decimal UnitPrice { get; set; }

    [Column("Zuschussanteil")]
    public int SubsidyPercent { get; set; }

    [Column("Menge")]
    public int Quantity { get; set; }

    /// <summary>Quantity leadership actually granted; null until decided, 0 means the line was struck.</summary>
    [Column("GenehmigteMenge")]
    public int? ApprovedQuantity { get; set; }

    [Column("Sortierung")]
    public int Sorting { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
