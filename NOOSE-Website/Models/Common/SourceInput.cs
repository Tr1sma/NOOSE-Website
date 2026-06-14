using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>
/// Eingabemodell zum Anlegen einer Quelle/eines Anhangs. Je nach <see cref="Typ"/> sind unterschiedliche
/// Felder relevant. Bei Upload wird der Datei-Inhalt bereits im Dialog in den Speicher gepuffert
/// (<see cref="DateiInhalt"/>), damit kein Browser-Stream über die Dialog-Lebensdauer hinaus benötigt wird.
/// </summary>
public class SourceInput
{
    public SourceType Type { get; set; } = SourceType.Link;

    public string Title { get; set; } = string.Empty;

    /// <summary>Freitext-Inhalt bzw. Notiz.</summary>
    public string? Description { get; set; }

    /// <summary>Ziel-URL bei <see cref="QuelleTyp.Link"/>.</summary>
    public string? Url { get; set; }

    // Interne Verknüpfung (QuelleTyp.Intern) bzw. Dokument-Verweis (QuelleTyp.Dokument).
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }

    /// <summary>
    /// Nur im Dialog verwendet (nicht persistiert): signalisiert, dass statt eines vorhandenen Dokuments
    /// ein neues im Volltext-Editor erstellt werden soll. Das Quellen-Panel navigiert dann zum Editor.
    /// </summary>
    public bool NewDocumentCreate { get; set; }

    // Upload (QuelleTyp.Upload).
    public byte[]? FileContent { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
}
