namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>
/// Speicher für Dateien der zentralen Datei-Bibliothek (Formulare, SOPs, Vorlagen – Phase 7),
/// geschützt außerhalb von wwwroot. Gleiche Typ-/Größen-Regeln wie die Quellen-Anhänge; eigener
/// Ordner (siehe <see cref="FileUploadOptions.BibliothekPfad"/>).
/// </summary>
public interface IBibliothekStorageService
{
    long MaxBytes { get; }

    bool IstErlaubterTyp(string contentType);

    /// <summary>Speichert den Inhalt und liefert den serverseitig vergebenen Dateinamen.</summary>
    Task<string> SpeichernAsync(Stream inhalt, string originalName, CancellationToken cancellationToken = default);

    Stream OeffnenLesen(string dateinameGespeichert);

    void Loeschen(string dateinameGespeichert);
}
