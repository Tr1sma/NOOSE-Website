using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Ein Waffen-Bestandseintrag einer Fraktion (Bezeichnung + optionale Menge) – Steckbrief-Kind.</summary>
[Table("FraktionWaffenbestaende")]
public class FraktionWaffenbestand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    [Column("Bezeichnung")]
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Menge als Freitext (z. B. „ca. 20", „mehrere").</summary>
    [Column("Menge")]
    public string? Menge { get; set; }
}
