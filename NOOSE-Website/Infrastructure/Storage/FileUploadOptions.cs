namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>File upload config.</summary>
public class FileUploadOptions
{
    /// <summary>People upload path.</summary>
    public string PeoplePath { get; set; } = "App_Data/uploads/personen";

    /// <summary>Factions upload path.</summary>
    public string FactionsPath { get; set; } = "App_Data/uploads/fraktionen";

    /// <summary>Max file size.</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Allowed image types.</summary>
    public string[] AllowedContentTypes { get; set; } =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    // ---- Attachments ----

    /// <summary>Sources upload path.</summary>
    public string SourcesPath { get; set; } = "App_Data/uploads/quellen";

    /// <summary>Max sources size.</summary>
    public long SourcesMaxBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Library upload path.</summary>
    public string LibraryPath { get; set; } = "App_Data/uploads/bibliothek";

    /// <summary>Allowed sources types.</summary>
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
