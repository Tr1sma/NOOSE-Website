using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>A faction weapon-stock entry (designation + optional quantity).</summary>
[Table("FraktionWaffenbestaende")]
public class FactionWeaponStock
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }
    [Column("Bezeichnung")]
    public string Designation { get; set; } = string.Empty;

    [Column("Menge")]
    public string? Quantity { get; set; }
}
