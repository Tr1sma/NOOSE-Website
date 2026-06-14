namespace NOOSE_Website.Models.Common;

/// <summary>Schlanke Zeile für die Dokumenten-Bibliothek/Auswahl (ohne den großen HTML-Body).</summary>
public record DocumentListItem(
    string Id,
    string Title,
    string? Category,
    bool IsClassified,
    DateTime Refreshed,
    bool Pinned);

/// <summary>Eine Akte, an die ein Dokument angehängt ist (für die „Angehängt an"-Liste im Viewer).</summary>
public record DocumentAttachment(string EntityType, string EntityId, string Display, string? Href);
