using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Input for a person-to-person relation.</summary>
public class RelationInput
{
    public string TargetPersonId { get; set; } = string.Empty;
    public RelationType Type { get; set; } = RelationType.Known;
    public string? Note { get; set; }
}

/// <summary>A relation from one person's perspective: the other person plus type.</summary>
public record RelationDisplay(
    string RelationId,
    RelationType Type,
    string? Note,
    string OtherPersonId,
    string OtherPersonName,
    string OtherPersonCaseNumber);
