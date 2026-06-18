using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Generic source/attachment; polymorphic by entity type and id.</summary>
[Table("Quellen")]
public class Source : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("Typ")]
    public SourceType Type { get; set; }

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Pinned; shown first.</summary>
    [Column("Angepinnt")]
    public bool Pinned { get; set; }

    [Column("Beschreibung")]
    public string? Description { get; set; }

    public string? Url { get; set; }

    [Column("ZielTyp")]
    public string? TargetType { get; set; }

    [Column("ZielId")]
    public string? TargetId { get; set; }

    [Column("DateinameGespeichert")]
    public string? FileNameSaved { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    [Column("GroesseBytes")]
    public long? SizeBytes { get; set; }

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
