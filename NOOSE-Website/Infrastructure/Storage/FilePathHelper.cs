namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>
/// Gemeinsamer Path-Traversal-Schutz für die Datei-Speicher (zuvor in FileStorageService und
/// QuellenStorageService identisch dupliziert). Lässt nur blanke Dateinamen zu und kombiniert sie
/// sicher mit dem Basispfad – eine Quelle der Wahrheit für die sicherheitskritische Prüfung.
/// </summary>
internal static class FilePathHelper
{
    public static string SafePath(string basePath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")
            || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException($"Ungültiger Dateiname: '{fileName}'.");
        }
        return Path.Combine(basePath, fileName);
    }
}
