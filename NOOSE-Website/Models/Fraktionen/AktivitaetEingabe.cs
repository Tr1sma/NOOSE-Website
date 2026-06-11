namespace NOOSE_Website.Models.Fraktionen;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Fraktions-Aktivität (Zeitstrahl-Eintrag).
/// <see cref="Zeitpunkt"/> ist der vom Nutzer gewählte Zeitpunkt der Aktion (Datum + Uhrzeit, lokal erfasst –
/// der Dienst speichert ihn als UTC). Dokument-Verknüpfungen laufen separat über die Quellen-Engine.
/// </summary>
public class AktivitaetEingabe
{
    /// <summary>Kurzer Titel (Pflicht), z. B. „Überfall Fleeca Bank".</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Art/Kategorie als Freitext mit Vorschlägen (z. B. „Raub", „Geiselnahme"); optional.</summary>
    public string? Art { get; set; }

    /// <summary>Zeitpunkt der Aktion (Datum + Uhrzeit).</summary>
    public DateTime Zeitpunkt { get; set; }

    /// <summary>Freitext-Beschreibung; optional.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Ort der Aktion; optional.</summary>
    public string? Ort { get; set; }
}
