namespace NOOSE_Website.Models.Common;

/// <summary>Eingabemodell für Anlegen/Bearbeiten eines Paragrafen im Gesetzbuch-Modul (Phase 7).</summary>
public class LawInput
{
    public string LawBook { get; set; } = string.Empty;
    public string Paragraph { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Sentence { get; set; }
}
