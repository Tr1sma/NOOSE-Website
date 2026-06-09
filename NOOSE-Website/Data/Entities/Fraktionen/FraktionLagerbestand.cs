namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>Ein Lager-Bestandseintrag einer Fraktion (Bezeichnung + optionale Menge) – Steckbrief-Kind.</summary>
public class FraktionLagerbestand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Menge als Freitext (z. B. „50 kg", „voll").</summary>
    public string? Menge { get; set; }
}
