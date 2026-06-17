using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Cases;

/// <summary>Umbrella case file bundling multiple records into one investigation via the generic link engine; assigned agents live in CaseAgent.</summary>
[Table("Vorgaenge")]
public class Case : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable unique case number (e.g. NOOSE-V-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Case type/category, free text with suggestions.</summary>
    [Column("Typ")]
    public string? Type { get; set; }

    public CaseStatus Status { get; set; } = CaseStatus.Open;

    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Zusammenfassung")]
    public string? Summary { get; set; }

    [Column("Abschlussvermerk")]
    public string? ClosingNote { get; set; }

    /// <summary>Set once the status moves to completed/archived.</summary>
    [Column("AbgeschlossenAm")]
    public DateTime? CompletedAt { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Classified: list/detail visible to leadership/admin only.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    public List<CaseAgent> Agents { get; set; } = new();

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
