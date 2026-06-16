using Microsoft.AspNetCore.Components.Routing;

namespace NOOSE_Website.Navigation;

/// <summary>Policy-gated drawer section an entry belongs to.</summary>
public enum NavSection
{
    Primary,
    Akten,
    VerwaltungFreigaben,
    VerwaltungFuehrung,
    VerwaltungAdmin,
    Partner,
}

/// <summary>Single drawer entry; the catalog is the source for label/icon/route/order.</summary>
public sealed record NavEntry(
    string Key,
    string Route,
    string Icon,
    string Label,
    NavSection Section,
    NavLinkMatch Match = NavLinkMatch.Prefix,
    string? BadgeKey = null);
