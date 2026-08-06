namespace NOOSE_Website.Models.Enums;

/// <summary>Categories of information leaked during an abduction; combinable bitmask.</summary>
[Flags]
public enum LeakCategory
{
    None = 0,
    AgentIdentities = 1,
    Operations = 2,
    Informants = 4,
    Safehouses = 8,
    ClassifiedDocuments = 16,
    OrgStructure = 32,
    Other = 64,
}

/// <summary>Display labels and flag decomposition.</summary>
public static class LeakCategoryDisplay
{
    public static string Name(LeakCategory category) => category switch
    {
        LeakCategory.AgentIdentities => "Agenten-Identitäten",
        LeakCategory.Operations => "Laufende Operationen",
        LeakCategory.Informants => "Informanten",
        LeakCategory.Safehouses => "Safehouses / Standorte",
        LeakCategory.ClassifiedDocuments => "VS-Dokumente",
        LeakCategory.OrgStructure => "Organisationsstruktur",
        LeakCategory.Other => "Sonstiges",
        _ => "—",
    };

    /// <summary>Every selectable flag, excluding None.</summary>
    public static readonly IReadOnlyList<LeakCategory> All = new[]
    {
        LeakCategory.AgentIdentities,
        LeakCategory.Operations,
        LeakCategory.Informants,
        LeakCategory.Safehouses,
        LeakCategory.ClassifiedDocuments,
        LeakCategory.OrgStructure,
        LeakCategory.Other,
    };

    /// <summary>Split a combined value into its set flags.</summary>
    public static IEnumerable<LeakCategory> Flags(LeakCategory value) =>
        All.Where(flag => value.HasFlag(flag));
}
