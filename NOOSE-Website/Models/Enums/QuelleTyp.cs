namespace NOOSE_Website.Models.Enums;

/// <summary>Art einer Quelle/eines Anhangs an einer Akte.</summary>
public enum QuelleTyp
{
    /// <summary>Hochgeladene Datei (geschützt außerhalb wwwroot abgelegt).</summary>
    Upload = 0,

    /// <summary>Externer Web-Link (Discord, Webseite …).</summary>
    Link = 1,

    /// <summary>Interne Verknüpfung auf eine andere Akte.</summary>
    Intern = 2,

    /// <summary>Reiner Freitext-Vermerk.</summary>
    Freitext = 3,
}

/// <summary>Anzeigetexte für den Quellen-Typ (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class QuelleTypAnzeige
{
    public static string Name(QuelleTyp typ) => typ switch
    {
        QuelleTyp.Upload => "Datei-Upload",
        QuelleTyp.Link => "Web-Link",
        QuelleTyp.Intern => "Interne Verknüpfung",
        QuelleTyp.Freitext => "Freitext",
        _ => "—",
    };

    public static readonly IReadOnlyList<QuelleTyp> Alle = new[]
    {
        QuelleTyp.Upload,
        QuelleTyp.Link,
        QuelleTyp.Intern,
        QuelleTyp.Freitext,
    };
}
