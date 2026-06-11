namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten einer Dokument-Vorlage.</summary>
public class DokumentVorlageEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public string? Kategorie { get; set; }

    /// <summary>HTML-Body der Vorlage (darf Platzhalter wie {{Name}} enthalten) – im Dienst bereinigt.</summary>
    public string InhaltHtml { get; set; } = string.Empty;

    public bool IstAktiv { get; set; } = true;
    public int Sortierung { get; set; }
}
