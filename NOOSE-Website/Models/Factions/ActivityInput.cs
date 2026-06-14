namespace NOOSE_Website.Models.Factions;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Fraktions-Aktivität (Zeitstrahl-Eintrag).
/// <see cref="Zeitpunkt"/> ist der vom Nutzer gewählte Zeitpunkt der Aktion (Datum + Uhrzeit, lokal erfasst –
/// der Dienst speichert ihn als UTC). Dokument-Verknüpfungen laufen separat über die Quellen-Engine.
/// </summary>
public class ActivityInput
{
    /// <summary>Kurzer Titel (Pflicht), z. B. „Überfall Fleeca Bank".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Art/Kategorie als Freitext mit Vorschlägen (z. B. „Raub", „Geiselnahme"); optional.</summary>
    public string? Kind { get; set; }

    /// <summary>Zeitpunkt der Aktion (Datum + Uhrzeit).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Freitext-Beschreibung; optional.</summary>
    public string? Description { get; set; }

    /// <summary>Ort der Aktion; optional.</summary>
    public string? Location { get; set; }
}
