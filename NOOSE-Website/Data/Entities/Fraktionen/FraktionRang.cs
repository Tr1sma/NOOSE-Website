namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Ein Rang innerhalb einer Fraktion (z. B. „Boss", „Soldat") – Steckbrief-Kind, hart gelöscht beim Entfernen.</summary>
public class FraktionRang
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Sortierung (höhere Ränge zuerst); rein anzeigeseitig.</summary>
    public int Reihenfolge { get; set; }
}
