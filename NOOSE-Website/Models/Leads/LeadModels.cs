using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Leads;

/// <summary>One proactive investigation lead. <see cref="Key"/> is deterministic and drives dismissal.</summary>
public sealed record Lead(
    LeadKind Kind,
    string Key,
    string Title,
    string Detail,
    int Score,
    bool Classified,
    string PrimaryType,
    string PrimaryId,
    string PrimaryName,
    string? PrimaryHref,
    string? SecondaryType = null,
    string? SecondaryId = null,
    string? SecondaryName = null,
    string? SecondaryHref = null);

/// <summary>Leads of one kind, for grouped display.</summary>
public sealed record LeadGroup(LeadKind Kind, IReadOnlyList<Lead> Leads);
