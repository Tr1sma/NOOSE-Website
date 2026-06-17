using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>An uploaded file in the central library; stored outside wwwroot, served only via a protected endpoint.</summary>
[Table("BibliothekDateien")]
public class LibraryFile : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    [Column("Kategorie")]
    public string? Category { get; set; }

    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Server-assigned file name in the library folder (GUID + safe extension).</summary>
    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Classified: leadership-only.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    /// <summary>TRU-only classification; mutually exclusive with the other levels (set via Classification).</summary>
    [Column("IstVerschlusssacheTRU")]
    public bool IsTRUClassified { get; set; }

    /// <summary>HRB-only classification; mutually exclusive with the other levels (set via Classification).</summary>
    [Column("IstVerschlusssacheHRB")]
    public bool IsHRBClassified { get; set; }

    /// <summary>Unified classification level mapping to exactly one bool column (or none); leadership wins on read.</summary>
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

    /// <summary>True if any classification level is set (drives the lock icon).</summary>
    [NotMapped]
    public bool IsRestricted => IsClassified || IsTRUClassified || IsHRBClassified;

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
