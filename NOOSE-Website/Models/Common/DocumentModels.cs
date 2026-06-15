using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Schlanke Zeile für die Dokumenten-Bibliothek/Auswahl (ohne den großen HTML-Body).</summary>
public record DocumentListItem(
    string Id,
    string Title,
    string? Category,
    DocumentClassification Classification,
    DateTime Refreshed,
    bool Pinned)
{
    /// <summary>True, wenn das Dokument irgendeiner Verschluss-Stufe unterliegt (für die Schloss-Anzeige).</summary>
    public bool IsRestricted => Classification != DocumentClassification.None;
}

/// <summary>Eine Akte, an die ein Dokument angehängt ist (für die „Angehängt an"-Liste im Viewer).</summary>
public record DocumentAttachment(string EntityType, string EntityId, string Display, string? Href);
