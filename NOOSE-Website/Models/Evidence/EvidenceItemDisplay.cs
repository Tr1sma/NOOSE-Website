using NOOSE_Website.Data.Entities.Evidence;

namespace NOOSE_Website.Models.Evidence;

/// <summary>Catalog item with its computed on-hand balance.</summary>
public record EvidenceItemDisplay(EvidenceItem Item, int OnHand)
{
    public bool HasImage => !string.IsNullOrEmpty(Item.ImageFileName);
}
