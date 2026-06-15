namespace NOOSE_Website.Models.Common;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten einer Dokument-Vorlage.</summary>
public class DocumentTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }

    /// <summary>HTML-Body der Vorlage (darf Platzhalter wie {{Name}} enthalten) – im Dienst bereinigt.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }
}
