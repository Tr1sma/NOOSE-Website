using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Generic source/attachment; polymorphic by entity type and id.</summary>
[Table("Quellen")]
public class Source : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Parent entity type.</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Parent entity key.</summary>
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("Typ")]
    public SourceType Type { get; set; }

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Pinned; shown first.</summary>
    [Column("Angepinnt")]
    public bool Pinned { get; set; }

    /// <summary>Free-text notes.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Link URL.</summary>
    public string? Url { get; set; }

    /// <summary>Internal link target type.</summary>
    [Column("ZielTyp")]
    public string? TargetType { get; set; }

    /// <summary>Internal link target key.</summary>
    [Column("ZielId")]
    public string? TargetId { get; set; }

    // ---- file metadata ----
    [Column("DateinameGespeichert")]
    public string? FileNameSaved { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    [Column("GroesseBytes")]
    public long? SizeBytes { get; set; }

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
