using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Lightweight document-library row without the HTML body.</summary>
public record DocumentListItem(
    string Id,
    string Title,
    string? Category,
    DocumentClassification Classification,
    DateTime Refreshed,
    bool Pinned)
{
    public bool IsRestricted => Classification != DocumentClassification.None;
}

/// <summary>A record a document is attached to.</summary>
public record DocumentAttachment(string EntityType, string EntityId, string Display, string? Href);
