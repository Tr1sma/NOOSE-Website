namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>
/// Speicher für Dateien der zentralen Datei-Bibliothek (Formulare, SOPs, Vorlagen – Phase 7),
/// geschützt außerhalb von wwwroot. Gleiche Typ-/Größen-Regeln wie die Quellen-Anhänge; eigener
/// Ordner (siehe <see cref="FileUploadOptions.BibliothekPfad"/>).
/// </summary>
public interface ILibraryStorageService
{
    long MaxBytes { get; }

    bool IsAllowedType(string contentType);

    /// <summary>Speichert den Inhalt und liefert den serverseitig vergebenen Dateinamen.</summary>
    Task<string> SaveAsync(Stream content, string originalName, CancellationToken cancellationToken = default);

    Stream OpenRead(string fileNameSaved);

    void Delete(string fileNameSaved);
}
