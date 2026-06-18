using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>A taskforce as a full record; members are Agents, with no person members and no classification.</summary>
[Table("Taskforces")]
public class Taskforce : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable unique case number (e.g. NOOSE-TF-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Column("Zweck")]
    public string? Purpose { get; set; }

    [Column("Geltungsbereich")]
    public TaskforceScope Scope { get; set; } = TaskforceScope.InternalAgency;

    public TaskforceStatus Status { get; set; } = TaskforceStatus.Requested;

    [Column("Bemerkungen")]
    public string? Remarks { get; set; }

    /// <summary>Classified: leadership/admin only in list and detail.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    public List<TaskforceAgent> Agents { get; set; } = new();

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
