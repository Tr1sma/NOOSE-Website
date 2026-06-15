using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>Ein Lager-Bestandseintrag einer Fraktion (Bezeichnung + optionale Menge) – Steckbrief-Kind.</summary>
[Table("FraktionLagerbestaende")]
public class FactionInventory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }
    [Column("Bezeichnung")]
    public string Designation { get; set; } = string.Empty;

    /// <summary>Menge als Freitext (z. B. „50 kg", „voll").</summary>
    [Column("Menge")]
    public string? Quantity { get; set; }
}
