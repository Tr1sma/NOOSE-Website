using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Generic key/value system setting (one row per key); falls back to code default when absent.</summary>
[Table("SystemEinstellungen")]
public class SystemSetting : IAuditable
{
    [Column("Schluessel")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Value as text (bool as "true"/"false", colours as hex, file names raw).</summary>
    [Column("Wert")]
    public string? Value { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
