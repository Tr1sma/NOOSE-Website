using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Models.Factions;

/// <summary>Create/edit faction input; members are maintained via their own endpoints, not here.</summary>
public class FactionInput
{
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? Radio { get; set; }
    public string? Darkchat { get; set; }
    public string? IssuingTimes { get; set; }
    public string? Estate { get; set; }
    public string? RecognitionColor { get; set; }
    public string? Targets { get; set; }
    public string? Description { get; set; }
    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public DocumentClassification SecrecyLevel { get; set; } = DocumentClassification.None;

    /// <summary>State faction; never goes stale (recency stays "current").</summary>
    public bool IsStateFaction { get; set; }

    public int? EstimatedMemberCount { get; set; }

    public List<RankInput> Ranks { get; set; } = new();
    public List<StockInput> WeaponStock { get; set; } = new();
    public List<StockInput> Inventory { get; set; } = new();

    /// <summary>Drug routes as a generic multi-field; the extra field carries the note.</summary>
    public List<StockInput> DrugRoutes { get; set; } = new();

    /// <summary>Members captured at creation time; further maintained on the detail page.</summary>
    public List<MemberInput> Members { get; set; } = new();
}

public class RankInput
{
    /// <summary>Existing rank id; empty for new ranks. Drives rename detection on save.</summary>
    public string? Id { get; set; }
    public string Designation { get; set; } = string.Empty;
}

public class StockInput : IProfileMultiple
{
    public string Designation { get; set; } = string.Empty;
    public string? Quantity { get; set; }

    string IProfileMultiple.MainValue { get => Designation; set => Designation = value; }
    string? IProfileMultiple.Extra { get => Quantity; set => Quantity = value; }
}

/// <summary>Add/edit faction membership.</summary>
public class MemberInput
{
    public string PersonId { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public bool IsLead { get; set; }

    /// <summary>Display only; ignored by service.</summary>
    public string? PersonName { get; set; }

    /// <summary>Auto-creates a new person if PersonId is empty.</summary>
    public string? NewPersonName { get; set; }
}
