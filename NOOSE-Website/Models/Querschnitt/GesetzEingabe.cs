namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Eingabemodell für Anlegen/Bearbeiten eines Paragrafen im Gesetzbuch-Modul (Phase 7).</summary>
public class GesetzEingabe
{
    public string Gesetzbuch { get; set; } = string.Empty;
    public string Paragraf { get; set; } = string.Empty;
    public string Titel { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Strafmass { get; set; }
}
