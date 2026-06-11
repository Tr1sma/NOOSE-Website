namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten eines Bibliotheks-Dokuments.</summary>
public class DokumentEingabe
{
    public string Titel { get; set; } = string.Empty;
    public string? Kategorie { get; set; }

    /// <summary>HTML aus dem WYSIWYG-Editor – wird im Dienst serverseitig bereinigt.</summary>
    public string InhaltHtml { get; set; } = string.Empty;

    public bool IstVerschlusssache { get; set; }
}
