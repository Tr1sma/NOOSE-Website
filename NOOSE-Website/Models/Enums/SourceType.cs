namespace NOOSE_Website.Models.Enums;

/// <summary>Art einer Quelle/eines Anhangs an einer Akte.</summary>
public enum SourceType
{
    /// <summary>Hochgeladene Datei (geschützt außerhalb wwwroot abgelegt).</summary>
    Upload = 0,

    /// <summary>Externer Web-Link (Discord, Webseite …).</summary>
    Link = 1,

    /// <summary>Interne Verknüpfung auf eine andere Akte.</summary>
    Internal = 2,

    /// <summary>Reiner Freitext-Vermerk.</summary>
    FreeText = 3,

    /// <summary>Verweis auf ein Bibliotheks-Dokument (Ziel über <c>ZielTyp</c>/<c>ZielId</c>).</summary>
    Document = 4,
}

/// <summary>Anzeigetexte für den Quellen-Typ (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
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
