namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Konfiguration für den Datei-/Foto-Upload (Sektion „FileUpload" in appsettings.json).</summary>
public class FileUploadOptions
{
    /// <summary>Zielordner relativ zum ContentRoot (außerhalb von wwwroot) oder absoluter Pfad.</summary>
    public string PersonenPfad { get; set; } = "App_Data/uploads/personen";

    /// <summary>Maximale Dateigröße in Bytes (Standard 10 MB).</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Erlaubte Content-Types (nur Bilder).</summary>
    public string[] ErlaubteContentTypes { get; set; } =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    // ---- Quellen/Anhänge (Phase 3a): eigener Bereich, breiteres Typ-Set (Dokumente) ----

    /// <summary>Zielordner für Quellen-Anhänge relativ zum ContentRoot (außerhalb von wwwroot) oder absoluter Pfad.</summary>
    public string QuellenPfad { get; set; } = "App_Data/uploads/quellen";

    /// <summary>Maximale Dateigröße für Quellen-Anhänge in Bytes (Standard 25 MB).</summary>
    public long QuellenMaxBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Erlaubte Content-Types für Quellen-Anhänge (Dokumente + Bilder + Archive).</summary>
    public string[] ErlaubteQuellenContentTypes { get; set; } =
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
