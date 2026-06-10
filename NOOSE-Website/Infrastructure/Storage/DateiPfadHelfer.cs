namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>
/// Gemeinsamer Path-Traversal-Schutz für die Datei-Speicher (zuvor in FileStorageService und
/// QuellenStorageService identisch dupliziert). Lässt nur blanke Dateinamen zu und kombiniert sie
/// sicher mit dem Basispfad – eine Quelle der Wahrheit für die sicherheitskritische Prüfung.
/// </summary>
internal static class DateiPfadHelfer
{
    public static string SichererPfad(string basisPfad, string dateiname)
    {
        if (string.IsNullOrWhiteSpace(dateiname)
            || dateiname.Contains('/') || dateiname.Contains('\\') || dateiname.Contains("..")
            || Path.GetFileName(dateiname) != dateiname)
        {
            throw new InvalidOperationException($"Ungültiger Dateiname: '{dateiname}'.");
        }
        return Path.Combine(basisPfad, dateiname);
    }
}
