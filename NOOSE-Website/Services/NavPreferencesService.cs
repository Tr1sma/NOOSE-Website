using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Navigation;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="INavPreferencesService" />
public class NavPreferencesService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache) : INavPreferencesService
{
    private const int RecentsCap = 15;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public event Action? Changed;

    private static string CacheKey(string agentId) => $"nav:{agentId}";

    public async Task<NavPreferences> GetAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return new NavPreferences();
        }
        if (cache.TryGetValue(CacheKey(agentId), out NavPreferences? cached) && cached is not null)
        {
            return cached;
        }
        NavPreferences prefs;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var json = await db.Users.AsNoTracking()
                .Where(a => a.Id == agentId)
                .Select(a => a.NavPreferencesJson)
                .FirstOrDefaultAsync(cancellationToken);
            prefs = Deserialize(json);
        }
        catch
        {
            /* best effort */
            return new NavPreferences();
        }
        cache.Set(CacheKey(agentId), prefs, CacheDuration);
        return prefs;
    }

    public Task ToggleFavoriteAsync(string agentId, NavFavorite favorite, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p =>
        {
            var id = INavPreferencesService.FavoriteId(favorite);
            var existing = p.Favorites.FirstOrDefault(f => INavPreferencesService.FavoriteId(f) == id);
            if (existing is not null)
            {
                p.Favorites.Remove(existing);
            }
            else
            {
                p.Favorites.Add(favorite);
            }
        }, cancellationToken);

    public Task ReorderFavoritesAsync(string agentId, IReadOnlyList<string> orderedIds, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p =>
        {
            var byId = p.Favorites.ToDictionary(INavPreferencesService.FavoriteId);
            var reordered = new List<NavFavorite>();
            foreach (var id in orderedIds)
            {
                if (byId.Remove(id, out var f))
                {
                    reordered.Add(f);
                }
            }
            // any not listed keep at the end
            reordered.AddRange(p.Favorites.Where(f => byId.ContainsKey(INavPreferencesService.FavoriteId(f))));
            p.Favorites = reordered;
        }, cancellationToken);

    public Task SetHiddenAsync(string agentId, string key, bool hidden, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p =>
        {
            if (hidden)
            {
                p.HiddenKeys.Add(key);
            }
            else
            {
                p.HiddenKeys.Remove(key);
            }
        }, cancellationToken);

    public Task SetOrderAsync(string agentId, IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p => p.Order = orderedKeys.ToList(), cancellationToken);

    public Task SetStartRouteAsync(string agentId, string? route, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p => p.StartRoute = string.IsNullOrWhiteSpace(route) ? null : route, cancellationToken);

    // drawer/group state are self-updating in the UI, so no live-refresh broadcast
    public Task SetDrawerOpenAsync(string agentId, bool open, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p => p.DrawerOpen = open, cancellationToken, notify: false);

    public Task SetGroupCollapsedAsync(string agentId, string section, bool collapsed, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p =>
        {
            if (collapsed)
            {
                p.CollapsedGroups.Add(section);
            }
            else
            {
                p.CollapsedGroups.Remove(section);
            }
        }, cancellationToken, notify: false);

    // high-frequency: no broadcast (drawer does not show recents)
    public Task PushRecentAsync(string agentId, RecentItem item, CancellationToken cancellationToken = default)
        => MutateAsync(agentId, p =>
        {
            p.Recents.RemoveAll(r => item.EntityId is not null
                ? r.EntityType == item.EntityType && r.EntityId == item.EntityId
                : r.EntityId is null && r.Route == item.Route);
            p.Recents.Insert(0, item);
            if (p.Recents.Count > RecentsCap)
            {
                p.Recents.RemoveRange(RecentsCap, p.Recents.Count - RecentsCap);
            }
        }, cancellationToken, notify: false);

    // read-modify-write of the JSON column; ExecuteUpdate bypasses the read-only barrier (pure UI prefs)
    private async Task MutateAsync(string agentId, Action<NavPreferences> mutate, CancellationToken cancellationToken, bool notify = true)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var json = await db.Users.AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => a.NavPreferencesJson)
            .FirstOrDefaultAsync(cancellationToken);
        var prefs = Deserialize(json);
        mutate(prefs);
        var updated = JsonSerializer.Serialize(prefs);
        await db.Users.Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.NavPreferencesJson, updated), cancellationToken);
        cache.Set(CacheKey(agentId), prefs, CacheDuration);
        if (notify)
        {
            Changed?.Invoke();
        }
    }

    private static NavPreferences Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new NavPreferences()
            : JsonSerializer.Deserialize<NavPreferences>(json) ?? new NavPreferences();
}
