using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Evidence;

/// <summary>Form model for creating/editing an evidence-room entry.</summary>
public class EvidenceEntryInput
{
    public EvidenceEntryType Type { get; set; } = EvidenceEntryType.Deposit;

    /// <summary>Owner record type: "NOOSE" / nameof(Agent) / nameof(Person).</summary>
    public string OwnerType { get; set; } = "NOOSE";
    public string? OwnerId { get; set; }

    /// <summary>UI-only: resolved owner label shown in the picker.</summary>
    public string? OwnerDisplay { get; set; }

    public string HandlerAgentId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    /// <summary>Item positions; each existing (ItemId) or new (ItemName → auto-created on save).</summary>
    public List<EvidenceLineInput> Lines { get; set; } = new();
}

/// <summary>One item position in the editor; ItemId set for an existing item, else ItemName triggers auto-create.</summary>
public class EvidenceLineInput
{
    public string? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}
