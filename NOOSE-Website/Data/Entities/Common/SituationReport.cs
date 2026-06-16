using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Archived monthly situation report; frozen stats snapshot.</summary>
[Table("Lageberichte")]
public class SituationReport : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Report year.</summary>
    [Column("Jahr")]
    public int Year { get; set; }

    /// <summary>Report month (1–12).</summary>
    [Column("Monat")]
    public int Month { get; set; }

    /// <summary>Display title.</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Frozen stats snapshot (JSON).</summary>
    public string SnapshotJson { get; set; } = string.Empty;

    // ---- IAuditable ----
    // created = report date
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
