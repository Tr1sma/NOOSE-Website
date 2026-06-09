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
}
