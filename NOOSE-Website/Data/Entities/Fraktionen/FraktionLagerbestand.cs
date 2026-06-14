using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Ein Lager-Bestandseintrag einer Fraktion (Bezeichnung + optionale Menge) – Steckbrief-Kind.</summary>
[Table("FraktionLagerbestaende")]
public class FraktionLagerbestand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    [Column("Bezeichnung")]
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Menge als Freitext (z. B. „50 kg", „voll").</summary>
    [Column("Menge")]
    public string? Menge { get; set; }
}
