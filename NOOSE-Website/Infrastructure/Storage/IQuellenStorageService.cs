namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>
/// Speicher für Quellen-Anhänge (Dokumente/PDF/Bilder …), geschützt außerhalb von wwwroot. Analog zu
/// <see cref="IFileStorageService"/>, aber mit eigenem Pfad und breiterem Typ-Set. Dateinamen werden
/// serverseitig vergeben; Lese-/Löschzugriffe lassen nur reine Dateinamen zu (Schutz vor Path-Traversal).
/// </summary>
public interface IQuellenStorageService
{
    /// <summary>Maximale erlaubte Dateigröße in Bytes.</summary>
    long MaxBytes { get; }

    /// <summary>Prüft, ob ein Content-Type erlaubt ist.</summary>
    bool IstErlaubterTyp(string contentType);

    /// <summary>Speichert den Inhalt und liefert den serverseitig vergebenen Dateinamen zurück. Die Endung wird sicher aus dem Originalnamen übernommen.</summary>
    Task<string> SpeichernAsync(Stream inhalt, string originalName, CancellationToken cancellationToken = default);

    /// <summary>Öffnet eine gespeicherte Datei zum Lesen. Der Aufrufer/Endpoint entsorgt den Stream.</summary>
    Stream OeffnenLesen(string dateinameGespeichert);

    /// <summary>Löscht eine gespeicherte Datei (falls vorhanden).</summary>
    void Loeschen(string dateinameGespeichert);
}
