using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>The published surfaces the public search groups its hits by.</summary>
/// <remarks>
/// A group key, nothing more. Deliberately no parser and no query parameter: the public search is one box, so no
/// attacker-chosen value ever has to be turned back into a member — which is the whole reason the situation level
/// needed a stable ASCII key next to its label and this does not.
/// </remarks>
public enum PublicSearchArea
{
    Fahndung = 0,
    Organisationen = 1,
    Presse = 2,
    Warnungen = 3,
    Berichte = 4,
    Information = 5,
    Recht = 6,
}

/// <summary>German label and icon of a search group.</summary>
public static class PublicSearchAreaDisplay
{
    /// <summary>Fixed display order. The groups must not reshuffle as caches expire.</summary>
    public static readonly IReadOnlyList<PublicSearchArea> All =
    [
        PublicSearchArea.Fahndung,
        PublicSearchArea.Organisationen,
        PublicSearchArea.Presse,
        PublicSearchArea.Warnungen,
        PublicSearchArea.Berichte,
        PublicSearchArea.Information,
        PublicSearchArea.Recht,
    ];

    public static string Name(PublicSearchArea area) => area switch
    {
        PublicSearchArea.Fahndung => "Fahndung",
        PublicSearchArea.Organisationen => "Organisationen",
        PublicSearchArea.Presse => "Pressemitteilungen",
        PublicSearchArea.Warnungen => "Warnungen",
        PublicSearchArea.Berichte => "Lageberichte",
        PublicSearchArea.Information => "Information",
        PublicSearchArea.Recht => "Recht",
        _ => "Sonstiges",
    };

    public static string Icon(PublicSearchArea area) => area switch
    {
        PublicSearchArea.Fahndung => Icons.Material.Filled.PersonSearch,
        PublicSearchArea.Organisationen => Icons.Material.Filled.Groups,
        PublicSearchArea.Presse => Icons.Material.Filled.Feed,
        PublicSearchArea.Warnungen => Icons.Material.Filled.Campaign,
        PublicSearchArea.Berichte => Icons.Material.Filled.Assessment,
        PublicSearchArea.Information => Icons.Material.Filled.MenuBook,
        PublicSearchArea.Recht => Icons.Material.Filled.Gavel,
        _ => Icons.Material.Filled.Search,
    };
}
