using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Category of an investigation lead (drives grouping, icon and label).</summary>
public enum LeadKind
{
    /// <summary>Two records likely connected (shared neighbours) but not yet linked.</summary>
    LinkPrediction = 0,

    /// <summary>A conflict edge appeared recently.</summary>
    NewConflict = 1,

    /// <summary>A high-classification record whose data has gone stale.</summary>
    StaleHighClassification = 2,
}

/// <summary>Display labels + icons for lead kinds.</summary>
public static class LeadKindDisplay
{
    public static string Name(LeadKind kind) => kind switch
    {
        LeadKind.LinkPrediction => "Mögliche Verbindung",
        LeadKind.NewConflict => "Neuer Konflikt",
        LeadKind.StaleHighClassification => "Veraltete Einstufung",
        _ => "Hinweis",
    };

    public static string Icon(LeadKind kind) => kind switch
    {
        LeadKind.LinkPrediction => Icons.Material.Filled.Hub,
        LeadKind.NewConflict => Icons.Material.Filled.Bolt,
        LeadKind.StaleHighClassification => Icons.Material.Filled.Update,
        _ => Icons.Material.Filled.Lightbulb,
    };
}
