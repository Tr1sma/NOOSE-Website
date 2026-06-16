using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Releases a record (or a single child item) to one partner agency for read-only access.</summary>
[Table("PartnerFreigaben")]
public class PartnerShare : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Released entity CLR type name (nameof), polymorphic.</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Released entity id.</summary>
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Target partner agency.</summary>
    [Column("Behoerde")]
    public PartnerAgency Agency { get; set; }

    /// <summary>Whole record incl. all current and future children; false = shell only, children released individually.</summary>
    [Column("InklusiveKinder")]
    public bool IncludesChildren { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
