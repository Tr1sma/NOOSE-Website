using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Navigation;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for NavPreferencesService against in-memory SQLite.</summary>
public sealed class NavPreferencesServiceTests : IDisposable
{
    private readonly SqliteTestContext _ctx = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private NavPreferencesService NewService() => new(_ctx.Factory, _cache);

    public void Dispose() => _ctx.Dispose();

    // Persist an Agent row so ExecuteUpdate mutations hit an existing record.
    private void SeedAgent(string id, NavPreferences? prefs = null)
    {
        using var db = _ctx.NewContext();
        var a = Seed.Agent(id);
        if (prefs is not null)
        {
            a.NavPreferencesJson = JsonSerializer.Serialize(prefs);
        }
        db.Users.Add(a);
        db.SaveChanges();
    }

    // Read the persisted JSON column straight from the DB, bypassing the service cache.
    private NavPreferences Stored(string id)
    {
        using var db = _ctx.NewContext();
        var json = db.Users.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.NavPreferencesJson)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(json)
            ? new NavPreferences()
            : JsonSerializer.Deserialize<NavPreferences>(json)!;
    }

    private static NavFavorite PageFav(string key, string label = "L", string route = "/r", string icon = "i")
        => new("page", key, null, null, label, route, icon);

    private static NavFavorite RecordFav(string entityType, string entityId, string label = "L", string route = "/r", string icon = "i")
        => new("record", null, entityType, entityId, label, route, icon);

    private static RecentItem PageRecent(string route, string label = "L")
        => new(route, label, "i", null, null, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

    private static RecentItem RecordRecent(string entityType, string entityId, string route = "/r")
        => new(route, "L", "i", entityType, entityId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

    // ---------------------------------------------------------------- GetAsync

    [Fact]
    public async Task GetAsync_ReturnsEmptyDefaults_WhenAgentIdBlank()
    {
        var svc = NewService();

        var prefs = await svc.GetAsync("");

        Assert.Empty(prefs.Favorites);
        Assert.Null(prefs.StartRoute);
        Assert.True(prefs.DrawerOpen);
        Assert.Equal(1, prefs.Version);
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenAgentHasNoStoredJson()
    {
        SeedAgent("a1");
        var svc = NewService();

        var prefs = await svc.GetAsync("a1");

        Assert.Empty(prefs.Favorites);
        Assert.Empty(prefs.HiddenKeys);
        Assert.Null(prefs.StartRoute);
    }

    [Fact]
    public async Task GetAsync_ReturnsDeserializedPreferences_FromStoredJson()
    {
        var seeded = new NavPreferences
        {
            StartRoute = "/personen",
            Favorites = { PageFav("people", "Personen", "/personen") },
        };
        seeded.HiddenKeys.Add("graph");
        SeedAgent("a1", seeded);
        var svc = NewService();

        var prefs = await svc.GetAsync("a1");

        Assert.Equal("/personen", prefs.StartRoute);
        Assert.Single(prefs.Favorites);
        Assert.Equal("people", prefs.Favorites[0].Key);
        Assert.Contains("graph", prefs.HiddenKeys);
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenAgentDoesNotExist()
    {
        var svc = NewService();

        var prefs = await svc.GetAsync("ghost");

        Assert.Empty(prefs.Favorites);
    }

    [Fact]
    public async Task GetAsync_ServesCachedValue_IgnoringLaterDbChange()
    {
        var seeded = new NavPreferences { StartRoute = "/first" };
        SeedAgent("a1", seeded);
        var svc = NewService();

        var first = await svc.GetAsync("a1");
        Assert.Equal("/first", first.StartRoute);

        // Mutate the DB behind the service's back; cache still holds the old value.
        using (var db = _ctx.NewContext())
        {
            var changed = new NavPreferences { StartRoute = "/second" };
            await db.Users.Where(a => a.Id == "a1")
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.NavPreferencesJson, JsonSerializer.Serialize(changed)));
        }

        var second = await svc.GetAsync("a1");
        Assert.Equal("/first", second.StartRoute);
    }

    // ---------------------------------------------------------------- ToggleFavoriteAsync

    [Fact]
    public async Task ToggleFavoriteAsync_AddsFavorite_WhenNotPresent()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.ToggleFavoriteAsync("a1", PageFav("people", "Personen", "/personen"));

        var stored = Stored("a1");
        Assert.Single(stored.Favorites);
        Assert.Equal("people", stored.Favorites[0].Key);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_RemovesFavorite_WhenSameIdAlreadyPresent()
    {
        var seeded = new NavPreferences { Favorites = { RecordFav("Person", "p1") } };
        SeedAgent("a1", seeded);
        var svc = NewService();

        // A different label/route but same id -> treated as the same favorite and removed.
        await svc.ToggleFavoriteAsync("a1", RecordFav("Person", "p1", "Other", "/other"));

        Assert.Empty(Stored("a1").Favorites);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_DoesNotPersist_WhenAgentIdBlank()
    {
        var svc = NewService();

        // No throw, no persistence (guard short-circuits before DB access).
        await svc.ToggleFavoriteAsync("", PageFav("people"));

        Assert.Empty(Stored("").Favorites);
    }

    // ---------------------------------------------------------------- ReorderFavoritesAsync

    [Fact]
    public async Task ReorderFavoritesAsync_AppliesGivenOrder_AndKeepsUnlistedAtEnd()
    {
        var seeded = new NavPreferences
        {
            Favorites =
            {
                PageFav("a"),
                PageFav("b"),
                PageFav("c"),
            },
        };
        SeedAgent("a1", seeded);
        var svc = NewService();

        // List c,a explicitly; b is unlisted and must trail.
        await svc.ReorderFavoritesAsync("a1", new[] { "page:c", "page:a" });

        var order = Stored("a1").Favorites.Select(f => f.Key).ToList();
        Assert.Equal(new[] { "c", "a", "b" }, order);
    }

    // ---------------------------------------------------------------- SetHiddenAsync

    [Fact]
    public async Task SetHiddenAsync_AddsKey_WhenHiddenTrue()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.SetHiddenAsync("a1", "graph", hidden: true);

        Assert.Contains("graph", Stored("a1").HiddenKeys);
    }

    [Fact]
    public async Task SetHiddenAsync_RemovesKey_WhenHiddenFalse()
    {
        var seeded = new NavPreferences();
        seeded.HiddenKeys.Add("graph");
        SeedAgent("a1", seeded);
        var svc = NewService();

        await svc.SetHiddenAsync("a1", "graph", hidden: false);

        Assert.DoesNotContain("graph", Stored("a1").HiddenKeys);
    }

    // ---------------------------------------------------------------- SetOrderAsync

    [Fact]
    public async Task SetOrderAsync_PersistsProvidedKeyOrder()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.SetOrderAsync("a1", new[] { "cases", "people", "graph" });

        Assert.Equal(new[] { "cases", "people", "graph" }, Stored("a1").Order);
    }

    // ---------------------------------------------------------------- SetStartRouteAsync

    [Fact]
    public async Task SetStartRouteAsync_SetsRoute()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.SetStartRouteAsync("a1", "/statistik");

        Assert.Equal("/statistik", Stored("a1").StartRoute);
    }

    [Fact]
    public async Task SetStartRouteAsync_ClearsRoute_WhenWhitespace()
    {
        var seeded = new NavPreferences { StartRoute = "/statistik" };
        SeedAgent("a1", seeded);
        var svc = NewService();

        await svc.SetStartRouteAsync("a1", "   ");

        Assert.Null(Stored("a1").StartRoute);
    }

    // ---------------------------------------------------------------- SetDrawerOpenAsync

    [Fact]
    public async Task SetDrawerOpenAsync_PersistsState()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.SetDrawerOpenAsync("a1", open: false);

        Assert.False(Stored("a1").DrawerOpen);
    }

    // ---------------------------------------------------------------- SetGroupCollapsedAsync

    [Fact]
    public async Task SetGroupCollapsedAsync_AddsSection_WhenCollapsed()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.SetGroupCollapsedAsync("a1", "Akten", collapsed: true);

        Assert.Contains("Akten", Stored("a1").CollapsedGroups);
    }

    [Fact]
    public async Task SetGroupCollapsedAsync_RemovesSection_WhenExpanded()
    {
        var seeded = new NavPreferences();
        seeded.CollapsedGroups.Add("Akten");
        SeedAgent("a1", seeded);
        var svc = NewService();

        await svc.SetGroupCollapsedAsync("a1", "Akten", collapsed: false);

        Assert.DoesNotContain("Akten", Stored("a1").CollapsedGroups);
    }

    // ---------------------------------------------------------------- PushRecentAsync

    [Fact]
    public async Task PushRecentAsync_InsertsItemAtFront()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.PushRecentAsync("a1", PageRecent("/a", "First"));
        await svc.PushRecentAsync("a1", PageRecent("/b", "Second"));

        var recents = Stored("a1").Recents;
        Assert.Equal(2, recents.Count);
        Assert.Equal("/b", recents[0].Route);
        Assert.Equal("/a", recents[1].Route);
    }

    [Fact]
    public async Task PushRecentAsync_DedupesByEntity_MovingToFront()
    {
        SeedAgent("a1");
        var svc = NewService();

        await svc.PushRecentAsync("a1", RecordRecent("Person", "p1", "/personen/p1"));
        await svc.PushRecentAsync("a1", RecordRecent("Person", "p2", "/personen/p2"));
        await svc.PushRecentAsync("a1", RecordRecent("Person", "p1", "/personen/p1"));

        var recents = Stored("a1").Recents;
        Assert.Equal(2, recents.Count);
        Assert.Equal("p1", recents[0].EntityId);
        Assert.Equal("p2", recents[1].EntityId);
    }

    [Fact]
    public async Task PushRecentAsync_CapsAtFifteen_KeepingNewest()
    {
        SeedAgent("a1");
        var svc = NewService();

        for (var i = 0; i < 20; i++)
        {
            await svc.PushRecentAsync("a1", PageRecent($"/r{i}", $"L{i}"));
        }

        var recents = Stored("a1").Recents;
        Assert.Equal(15, recents.Count);
        Assert.Equal("/r19", recents[0].Route);
        Assert.Equal("/r5", recents[^1].Route);
    }

    // ---------------------------------------------------------------- Changed event

    [Fact]
    public async Task Changed_Fires_OnNotifyingMutation()
    {
        SeedAgent("a1");
        var svc = NewService();
        var fired = false;
        svc.Changed += () => fired = true;

        await svc.ToggleFavoriteAsync("a1", PageFav("people"));

        Assert.True(fired);
    }

    [Fact]
    public async Task Changed_DoesNotFire_OnSilentMutation()
    {
        SeedAgent("a1");
        var svc = NewService();
        var fired = false;
        svc.Changed += () => fired = true;

        // Drawer/group/recents mutate with notify:false.
        await svc.SetDrawerOpenAsync("a1", open: false);
        await svc.SetGroupCollapsedAsync("a1", "Akten", collapsed: true);
        await svc.PushRecentAsync("a1", PageRecent("/a"));

        Assert.False(fired);
    }
}
