using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>A rank within a faction; hard-deleted when removed.</summary>
[Table("FraktionRaenge")]
public class FactionRank
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }
    [Column("Bezeichnung")]
    public string Designation { get; set; } = string.Empty;

    /// <summary>Display order (higher ranks first).</summary>
    [Column("Reihenfolge")]
    public int Order { get; set; }
}
