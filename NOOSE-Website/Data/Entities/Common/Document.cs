using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Reusable document in the central library; content stored as sanitized HTML, attached to records via generic sources.</summary>
[Table("Dokumente")]
public class Document : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    [Column("Kategorie")]
    public string? Category { get; set; }

    /// <summary>Server-side sanitized HTML content.</summary>
    [Column("InhaltHtml")]
    public string ContentHtml { get; set; } = string.Empty;

    /// <summary>Classified: leadership-only visibility.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    /// <summary>Classified for TRU only; mutually exclusive with the other classification levels.</summary>
    [Column("IstVerschlusssacheTRU")]
    public bool IsTRUClassified { get; set; }

    /// <summary>Classified for HRB only; mutually exclusive with the other classification levels.</summary>
    [Column("IstVerschlusssacheHRB")]
    public bool IsHRBClassified { get; set; }

    /// <summary>Unified classification level mapping onto exactly one of the bool columns (or none).</summary>
    [NotMapped]
    public DocumentClassification Classification
    {
        get => IsClassified ? DocumentClassification.Leadership
            : IsTRUClassified ? DocumentClassification.Tru
            : IsHRBClassified ? DocumentClassification.Hrb
            : DocumentClassification.None;
        set
        {
            IsClassified = value == DocumentClassification.Leadership;
            IsTRUClassified = value == DocumentClassification.Tru;
            IsHRBClassified = value == DocumentClassification.Hrb;
        }
    }

    /// <summary>True when any classification level is set (drives the lock icon).</summary>
    [NotMapped]
    public bool IsRestricted => IsClassified || IsTRUClassified || IsHRBClassified;

    /// <summary>Pinned to the top of the library; affects display order only.</summary>
    [Column("Angepinnt")]
    public bool Pinned { get; set; }

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
