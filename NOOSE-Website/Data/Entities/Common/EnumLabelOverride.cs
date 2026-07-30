using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>DB-backed display-name override for a code-defined value-list entry (enum member); the enum itself stays code-owned.</summary>
[Table("WertelistenLabels")]
public class EnumLabelOverride
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("Liste")]
    public string List { get; set; } = string.Empty;
    [Column("Schluessel")]
    public string Key { get; set; } = string.Empty;
    [Column("Anzeigename")]
    public string Label { get; set; } = string.Empty;
}
