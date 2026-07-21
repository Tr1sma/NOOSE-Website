using System.Text.Json;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for SavedSearchService over in-memory SQLite.</summary>
public class SavedSearchServiceTests
{
    private static SavedSearchService NewService(SqliteTestContext ctx) => new(ctx.Factory);

    // Seed an Agent so the SavedSearch -> Agent FK is always satisfiable.
    private static void SeedAgent(SqliteTestContext ctx, string agentId)
    {
        using var db = ctx.NewContext();
        db.Users.Add(Seed.Agent(agentId));
        db.SaveChanges();
    }

    // ---- GetForAgentAsync ----

    [Fact]
    public async Task GetForAgentAsync_ReturnsOnlyOwnSearches_OrderedByName()
    {
        using var ctx = new SqliteTestContext();
        SeedAgent(ctx, "a1");
        SeedAgent(ctx, "a2");
        using (var db = ctx.NewContext())
        {
            db.SavedSearch.Add(new SavedSearch { AgentId = "a1", Name = "Zulu", SearchParameterJson = "{}" });
            db.SavedSearch.Add(new SavedSearch { AgentId = "a1", Name = "Alpha", SearchParameterJson = "{}" });
            db.SavedSearch.Add(new SavedSearch { AgentId = "a2", Name = "Bravo", SearchParameterJson = "{}" });
            db.SaveChanges();
        }

        var svc = NewService(ctx);
        var result = await svc.GetForAgentAsync("a1");

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("a1", r.AgentId));
        Assert.Equal(new[] { "Alpha", "Zulu" }, result.Select(r => r.Name).ToArray());
    }

    [Fact]
    public async Task GetForAgentAsync_ReturnsEmpty_WhenNone()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        var result = await svc.GetForAgentAsync("nobody");

        Assert.Empty(result);
    }

    // ---- SaveAsync ----

    [Fact]
    public async Task SaveAsync_PersistsAndSerializesCriteria()
    {
        using var ctx = new SqliteTestContext();
        SeedAgent(ctx, "a1");
        var criteria = new SearchCriteria
        {
            Text = "gang",
            Categories = { "Person", "Faction" },
            TagIds = { "t1" },
            Fuzzy = true,
            MaxMode = true,
        };

        var svc = NewService(ctx);
        var entry = await svc.SaveAsync("a1", "Meine Suche", criteria, ClaimsPrincipalBuilder.Agent("a1"));

        Assert.False(string.IsNullOrWhiteSpace(entry.Id));
        Assert.Equal("a1", entry.AgentId);
        Assert.Equal("Meine Suche", entry.Name);

        using var db = ctx.NewContext();
        var stored = await db.SavedSearch.FindAsync(entry.Id);
        Assert.NotNull(stored);
        Assert.Equal("Meine Suche", stored!.Name);

        var roundTrip = JsonSerializer.Deserialize<SearchCriteria>(stored.SearchParameterJson);
        Assert.NotNull(roundTrip);
        Assert.Equal("gang", roundTrip!.Text);
        Assert.True(roundTrip.Fuzzy);
        Assert.True(roundTrip.MaxMode);
        Assert.Equal(new[] { "Person", "Faction" }, roundTrip.Categories.ToArray());
        Assert.Equal(new[] { "t1" }, roundTrip.TagIds.ToArray());
    }

    [Fact]
    public async Task SaveAsync_TrimsName()
    {
        using var ctx = new SqliteTestContext();
        SeedAgent(ctx, "a1");

        var svc = NewService(ctx);
        var entry = await svc.SaveAsync("a1", "   Padded   ", new SearchCriteria(), ClaimsPrincipalBuilder.Agent("a1"));

        Assert.Equal("Padded", entry.Name);

        using var db = ctx.NewContext();
        var stored = await db.SavedSearch.FindAsync(entry.Id);
        Assert.Equal("Padded", stored!.Name);
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenNameBlank()
    {
        using var ctx = new SqliteTestContext();
        SeedAgent(ctx, "a1");

        var svc = NewService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync("a1", "   ", new SearchCriteria(), ClaimsPrincipalBuilder.Agent("a1")));

        using var db = ctx.NewContext();
        Assert.Empty(db.SavedSearch);
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenAgentIdBlank()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync("", "Valid Name", new SearchCriteria(), ClaimsPrincipalBuilder.Agent("a1")));

        using var db = ctx.NewContext();
        Assert.Empty(db.SavedSearch);
    }

    // ---- DeleteAsync ----

    [Fact]
    public async Task DeleteAsync_RemovesOwnSearch()
    {
        using var ctx = new SqliteTestContext();
        SeedAgent(ctx, "a1");
        var id = Guid.NewGuid().ToString();
        using (var db = ctx.NewContext())
        {
            db.SavedSearch.Add(new SavedSearch { Id = id, AgentId = "a1", Name = "ToDelete", SearchParameterJson = "{}" });
            db.SaveChanges();
        }

        var svc = NewService(ctx);
        await svc.DeleteAsync(id, "a1");

        using var check = ctx.NewContext();
        Assert.Null(await check.SavedSearch.FindAsync(id));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotRemoveOtherAgentsSearch()
    {
        using var ctx = new SqliteTestContext();
        SeedAgent(ctx, "owner");
        var id = Guid.NewGuid().ToString();
        using (var db = ctx.NewContext())
        {
            db.SavedSearch.Add(new SavedSearch { Id = id, AgentId = "owner", Name = "Owned", SearchParameterJson = "{}" });
            db.SaveChanges();
        }

        var svc = NewService(ctx);
        await svc.DeleteAsync(id, "intruder");

        using var check = ctx.NewContext();
        Assert.NotNull(await check.SavedSearch.FindAsync(id));
    }

    [Fact]
    public async Task DeleteAsync_NoOp_WhenIdUnknown()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        // Must not throw for a non-existent id.
        await svc.DeleteAsync("missing-id", "a1");

        using var db = ctx.NewContext();
        Assert.Empty(db.SavedSearch);
    }
}
