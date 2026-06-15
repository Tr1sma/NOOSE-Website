namespace NOOSE_Website.Models.Common;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten eines Bibliotheks-Dokuments.</summary>
public class DocumentInput
{
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }

    /// <summary>HTML aus dem WYSIWYG-Editor – wird im Dienst serverseitig bereinigt.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public bool IsClassified { get; set; }
}
