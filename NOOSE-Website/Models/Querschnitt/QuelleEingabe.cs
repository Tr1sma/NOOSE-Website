using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>
/// Eingabemodell zum Anlegen einer Quelle/eines Anhangs. Je nach <see cref="Typ"/> sind unterschiedliche
/// Felder relevant. Bei Upload wird der Datei-Inhalt bereits im Dialog in den Speicher gepuffert
/// (<see cref="DateiInhalt"/>), damit kein Browser-Stream über die Dialog-Lebensdauer hinaus benötigt wird.
/// </summary>
public class QuelleEingabe
{
    public QuelleTyp Typ { get; set; } = QuelleTyp.Link;

    public string Titel { get; set; } = string.Empty;

    /// <summary>Freitext-Inhalt bzw. Notiz.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Ziel-URL bei <see cref="QuelleTyp.Link"/>.</summary>
    public string? Url { get; set; }

    // Interne Verknüpfung (QuelleTyp.Intern).
    public string? ZielTyp { get; set; }
    public string? ZielId { get; set; }

    // Upload (QuelleTyp.Upload).
    public byte[]? DateiInhalt { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    public long GroesseBytes { get; set; }
}
