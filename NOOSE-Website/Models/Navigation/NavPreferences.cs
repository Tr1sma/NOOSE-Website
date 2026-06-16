namespace NOOSE_Website.Models.Navigation;

/// <summary>Per-account navigation preferences, persisted as JSON on the Agent.</summary>
public sealed class NavPreferences
{
    /// <summary>Pinned pages and records, in display order.</summary>
    public List<NavFavorite> Favorites { get; set; } = [];

    /// <summary>Catalog keys the user has hidden.</summary>
    public HashSet<string> HiddenKeys { get; set; } = [];

    /// <summary>Catalog keys in preferred order; absent keys keep their default position.</summary>
    public List<string> Order { get; set; } = [];

    /// <summary>Custom landing route after login; null = "/".</summary>
    public string? StartRoute { get; set; }

    /// <summary>Remembered drawer open state.</summary>
    public bool DrawerOpen { get; set; } = true;

    /// <summary>Section names the user has collapsed.</summary>
    public HashSet<string> CollapsedGroups { get; set; } = [];

    /// <summary>Recently visited pages/records, newest first, capped.</summary>
    public List<RecentItem> Recents { get; set; } = [];

    /// <summary>Schema version for future migrations.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>A pinned page or record in the favorites quick-access list.</summary>
public sealed record NavFavorite(
    string Kind,
    string? Key,
    string? EntityType,
    string? EntityId,
    string Label,
    string Route,
    string Icon);

/// <summary>An auto-tracked recently visited page or record.</summary>
public sealed record RecentItem(
    string Route,
    string Label,
    string Icon,
    string? EntityType,
    string? EntityId,
    DateTime VisitedAtUtc);
