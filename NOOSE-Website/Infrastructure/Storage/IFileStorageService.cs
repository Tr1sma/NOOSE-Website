namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>
/// Speichert hochgeladene Dateien geschützt außerhalb von wwwroot und liefert sie kontrolliert aus.
/// Dateinamen werden serverseitig vergeben; Lese-/Löschzugriffe lassen nur reine Dateinamen zu
/// (Schutz vor Path-Traversal).
/// </summary>
public interface IFileStorageService
{
    /// <summary>Maximale erlaubte Dateigröße in Bytes.</summary>
    long MaxBytes { get; }

    /// <summary>Prüft, ob ein Content-Type erlaubt ist.</summary>
    bool IsAllowedType(string contentType);

    /// <summary>Speichert den Inhalt und liefert den serverseitig vergebenen Dateinamen zurück.</summary>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Öffnet eine gespeicherte Datei zum Lesen. Der Aufrufer/Endpoint entsorgt den Stream.</summary>
    Stream OpenRead(string fileNameSaved);

    /// <summary>Löscht eine gespeicherte Datei (falls vorhanden).</summary>
    void Delete(string fileNameSaved);
}
