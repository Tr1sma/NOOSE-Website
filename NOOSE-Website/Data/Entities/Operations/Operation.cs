using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Operations;

/// <summary>An operation / mission report as a standalone event case file with assigned agents and classification.</summary>
[Table("Operationen")]
public class Operation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable unique case number (e.g. NOOSE-OP-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    [Column("Typ")]
    public string? Type { get; set; }

    public OperationStatus Status { get; set; } = OperationStatus.Planned;

    [Column("Ort")]
    public string? Location { get; set; }

    [Column("Beginn")]
    public DateTime? Start { get; set; }

    [Column("Ende")]
    public DateTime? End { get; set; }

    [Column("Ablauf")]
    public string? Expiry { get; set; }

    [Column("Ergebnis")]
    public string? Result { get; set; }

    [Column("Bemerkungen")]
    public string? Remarks { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Classified: visible only to leadership/admin.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    public List<OperationAgent> Agents { get; set; } = new();

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
