using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Admin-configurable recency-traffic-light thresholds per record type; falls back to code default when absent.</summary>
[Table("AktualitaetsSchwellen")]
public class RecencyThreshold : IAuditable
{
    [Column("AktenTyp")]
    public string RecordsType { get; set; } = string.Empty;

    /// <summary>Days without change before the light turns yellow.</summary>
    [Column("WarnungTage")]
    public int WarningDays { get; set; }

    /// <summary>Days without change before the light turns red.</summary>
    [Column("VeraltetTage")]
    public int StaleDays { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
