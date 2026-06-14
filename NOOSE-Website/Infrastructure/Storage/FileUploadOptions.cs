namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Konfiguration für den Datei-/Foto-Upload (Sektion „FileUpload" in appsettings.json).</summary>
public class FileUploadOptions
{
    /// <summary>Zielordner relativ zum ContentRoot (außerhalb von wwwroot) oder absoluter Pfad.</summary>
    public string PeoplePath { get; set; } = "App_Data/uploads/personen";

    /// <summary>Zielordner für Fraktions-Fotos (eigener Bereich, sonst gleiche Bild-Regeln wie Personen).</summary>
    public string FactionsPath { get; set; } = "App_Data/uploads/fraktionen";

    /// <summary>Maximale Dateigröße in Bytes (Standard 10 MB).</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Erlaubte Content-Types (nur Bilder).</summary>
    public string[] AllowedContentTypes { get; set; } =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    // ---- Quellen/Anhänge (Phase 3a): eigener Bereich, breiteres Typ-Set (Dokumente) ----

    /// <summary>Zielordner für Quellen-Anhänge relativ zum ContentRoot (außerhalb von wwwroot) oder absoluter Pfad.</summary>
    public string SourcesPath { get; set; } = "App_Data/uploads/quellen";

    /// <summary>Maximale Dateigröße für Quellen-Anhänge in Bytes (Standard 25 MB).</summary>
    public long SourcesMaxBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Zielordner für Dateien der zentralen Datei-Bibliothek (Phase 7); gleiche Typ-/Größen-Regeln wie Quellen.</summary>
    public string LibraryPath { get; set; } = "App_Data/uploads/bibliothek";

    /// <summary>Erlaubte Content-Types für Quellen-Anhänge (Dokumente + Bilder + Archive).</summary>
    public string[] AllowedSourcesContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "text/plain", "text/csv",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/zip",
    ];
}
