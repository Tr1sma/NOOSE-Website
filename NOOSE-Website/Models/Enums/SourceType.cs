namespace NOOSE_Website.Models.Enums;

/// <summary>Source/attachment type.</summary>
public enum SourceType
{
    /// <summary>Uploaded file.</summary>
    Upload = 0,

    /// <summary>External web link.</summary>
    Link = 1,

    /// <summary>Internal record link.</summary>
    Internal = 2,

    /// <summary>Free-text note.</summary>
    FreeText = 3,

    /// <summary>Library document reference.</summary>
    Document = 4,
}

/// <summary>Display labels.</summary>
public static class SourceTypeDisplay
{
    public static string Name(SourceType type) => type switch
    {
        SourceType.Upload => "Datei-Upload",
        SourceType.Link => "Web-Link",
        SourceType.Internal => "Interne Verknüpfung",
        SourceType.FreeText => "Freitext",
        SourceType.Document => "Dokument",
        _ => "—",
    };

    public static readonly IReadOnlyList<SourceType> All = new[]
    {
        SourceType.Upload,
        SourceType.Link,
        SourceType.Internal,
        SourceType.FreeText,
        SourceType.Document,
    };
}
