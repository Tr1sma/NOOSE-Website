using NOOSE_Website.Data.Entities.Evidence;

namespace NOOSE_Website.Models.Evidence;

/// <summary>Entry with resolved owner and handler plus its item positions.</summary>
public record EvidenceEntryDisplay(
    EvidenceEntry Entry,
    string OwnerDisplay,
    string? OwnerHref,
    string HandlerCodename,
    IReadOnlyList<EvidenceLineDisplay> Lines);

/// <summary>One resolved position: item name, whether that item carries an image, quantity.</summary>
public record EvidenceLineDisplay(string ItemId, string ItemName, bool HasImage, int Quantity);
