using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Eine Drogenroute einer Fraktion (Bezeichnung + optionale Notiz) – Steckbrief-Kind, analog zum Waffenbestand.</summary>
[Table("FraktionDrogenrouten")]
public class FraktionDrogenroute
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    [Column("Bezeichnung")]
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Notiz als Freitext (z. B. Droge, Übergabeort, Details zur Route).</summary>
    [Column("Notiz")]
    public string? Notiz { get; set; }
}
