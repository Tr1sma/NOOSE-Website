using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>A directionally-stored but bidirectionally-shown link between any two records (polymorphic by from/to type + id).</summary>
[Table("Verknuepfungen")]
public class Link : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("VonTyp")]
    public string SourceType { get; set; } = string.Empty;
    [Column("VonId")]
    public string SourceId { get; set; } = string.Empty;

    [Column("NachTyp")]
    public string TargetType { get; set; } = string.Empty;
    [Column("NachId")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Optional relationship label/note.</summary>
    public string? Label { get; set; }

    /// <summary>Link kind: general (default) or organizational relationship (conflict/alliance).</summary>
    [Column("Art")]
    public LinkKind Kind { get; set; } = LinkKind.Default;

    /// <summary>System-maintained auto link; not manually editable, removed when its basis disappears.</summary>
    [Column("Automatisch")]
    public bool Automatic { get; set; }

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
