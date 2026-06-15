using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>Ein Waffen-Bestandseintrag einer Fraktion (Bezeichnung + optionale Menge) – Steckbrief-Kind.</summary>
[Table("FraktionWaffenbestaende")]
public class FactionWeaponStock
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }
    [Column("Bezeichnung")]
    public string Designation { get; set; } = string.Empty;

    /// <summary>Menge als Freitext (z. B. „ca. 20", „mehrere").</summary>
    [Column("Menge")]
    public string? Quantity { get; set; }
}
