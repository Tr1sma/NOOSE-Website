using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Ein Rang innerhalb einer Fraktion (z. B. „Boss", „Soldat") – Steckbrief-Kind, hart gelöscht beim Entfernen.</summary>
[Table("FraktionRaenge")]
public class FraktionRang
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    [Column("Bezeichnung")]
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Sortierung (höhere Ränge zuerst); rein anzeigeseitig.</summary>
    [Column("Reihenfolge")]
    public int Reihenfolge { get; set; }
}
