using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>Eine Drogenroute einer Fraktion (Bezeichnung + optionale Notiz) – Steckbrief-Kind, analog zum Waffenbestand.</summary>
[Table("FraktionDrogenrouten")]
public class FactionDrugRoute
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }
    [Column("Bezeichnung")]
    public string Designation { get; set; } = string.Empty;

    /// <summary>Notiz als Freitext (z. B. Droge, Übergabeort, Details zur Route).</summary>
    [Column("Notiz")]
    public string? Note { get; set; }
}
