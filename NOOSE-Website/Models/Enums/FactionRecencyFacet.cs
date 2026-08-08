using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>The four faction sections that carry their own freshness stamp; the oldest of them drives the record's light.</summary>
public enum FactionRecencyFacet
{
    /// <summary>Membership list.</summary>
    Members = 0,

    /// <summary>Weapon stock, inventory and drug routes.</summary>
    Stock = 1,

    /// <summary>Activities linked to the faction.</summary>
    Activities = 2,

    /// <summary>Person docs linked to the faction.</summary>
    Docs = 3,
}

/// <summary>Display labels.</summary>
public static class FactionRecencyFacetDisplay
{
    /// <summary>All facets in display order.</summary>
    public static readonly FactionRecencyFacet[] All =
    {
        FactionRecencyFacet.Members, FactionRecencyFacet.Stock,
        FactionRecencyFacet.Activities, FactionRecencyFacet.Docs,
    };

    public static string Name(FactionRecencyFacet facet) => facet switch
    {
        FactionRecencyFacet.Members => "Mitglieder",
        FactionRecencyFacet.Stock => "Bestände",
        FactionRecencyFacet.Activities => "Aktivitäten",
        FactionRecencyFacet.Docs => "Doks",
        _ => "Unbekannt",
    };

    public static string Icon(FactionRecencyFacet facet) => facet switch
    {
        FactionRecencyFacet.Members => Icons.Material.Filled.Groups,
        FactionRecencyFacet.Stock => Icons.Material.Filled.Inventory2,
        FactionRecencyFacet.Activities => Icons.Material.Filled.Bolt,
        FactionRecencyFacet.Docs => Icons.Material.Filled.Description,
        _ => Icons.Material.Filled.HelpOutline,
    };

    /// <summary>Section slug on the faction detail page, for deep links from the freshness breakdown.</summary>
    public static string Slug(FactionRecencyFacet facet) => facet switch
    {
        FactionRecencyFacet.Members => "mitglieder",
        FactionRecencyFacet.Stock => "bestaende",
        FactionRecencyFacet.Activities => "aktivitaeten",
        FactionRecencyFacet.Docs => "doks",
        _ => "stammdaten",
    };
}
