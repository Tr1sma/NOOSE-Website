namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Ein Waffen-Bestandseintrag einer Fraktion (Bezeichnung + optionale Menge) – Steckbrief-Kind.</summary>
public class FraktionWaffenbestand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Menge als Freitext (z. B. „ca. 20", „mehrere").</summary>
    public string? Menge { get; set; }
}
