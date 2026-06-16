using NOOSE_Website.Models.Navigation;

namespace NOOSE_Website.Services;

/// <summary>Per-account navigation preferences: favorites, hidden/order, recents, drawer/group state.</summary>
public interface INavPreferencesService
{
    /// <summary>Fires after this circuit's user changed their preferences.</summary>
    event Action? Changed;

    /// <summary>Current preferences for an account (cached); empty defaults when unset.</summary>
    Task<NavPreferences> GetAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Pin or unpin a favorite (page or record), matched by its stable id.</summary>
    Task ToggleFavoriteAsync(string agentId, NavFavorite favorite, CancellationToken cancellationToken = default);

    /// <summary>Reorder favorites by their stable ids.</summary>
    Task ReorderFavoritesAsync(string agentId, IReadOnlyList<string> orderedIds, CancellationToken cancellationToken = default);

    /// <summary>Hide or show a catalog entry by key.</summary>
    Task SetHiddenAsync(string agentId, string key, bool hidden, CancellationToken cancellationToken = default);

    /// <summary>Set the personal entry order (catalog keys).</summary>
    Task SetOrderAsync(string agentId, IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken = default);

    /// <summary>Set the custom landing route; null/empty clears it.</summary>
    Task SetStartRouteAsync(string agentId, string? route, CancellationToken cancellationToken = default);

    /// <summary>Remember the drawer open state.</summary>
    Task SetDrawerOpenAsync(string agentId, bool open, CancellationToken cancellationToken = default);

    /// <summary>Remember a section's collapsed state.</summary>
    Task SetGroupCollapsedAsync(string agentId, string section, bool collapsed, CancellationToken cancellationToken = default);

    /// <summary>Record a visited page/record (dedupes + caps).</summary>
    Task PushRecentAsync(string agentId, RecentItem item, CancellationToken cancellationToken = default);

    /// <summary>Stable id for a favorite (used for reordering).</summary>
    static string FavoriteId(NavFavorite f)
        => f.Kind == "page" ? $"page:{f.Key}" : $"record:{f.EntityType}:{f.EntityId}";
}
