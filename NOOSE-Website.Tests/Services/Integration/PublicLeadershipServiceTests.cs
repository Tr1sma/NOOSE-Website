using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The one outward surface that names agents: nothing goes live without a release per entry.</summary>
public sealed class PublicLeadershipServiceTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal Supervision()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static ClaimsPrincipal Agent()
        => ClaimsPrincipalBuilder.Agent("agent").WithRank(Rank.SpecialAgent).Build();

    private sealed record Host(
        PublicLeadershipService Service,
        PublicModuleService Modules,
        IPublicLeadershipPhotoStorageService Storage,
        TestDbContextFactory Factory);

    private static Host NewHost(SqliteTestContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ctx.Connection).Options;
        var factory = new TestDbContextFactory(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var modules = new PublicModuleService(factory, cache);
        var storage = Substitute.For<IPublicLeadershipPhotoStorageService>();
        storage.IsAllowedType(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0).StartsWith("image/"));
        storage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("kopie.jpg");
        return new Host(new PublicLeadershipService(factory, modules, storage, cache), modules, storage, factory);
    }

    private static async Task<SqliteTestContext> SeededAsync(bool moduleOn = true)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Leadership)).IsEnabled = moduleOn;

        db.Users.Add(Seed.Agent("chef", Rank.Director, configure: a => a.Codename = "Falcon"));
        db.Users.Add(Seed.Agent("aufsicht2", Rank.SupervisorySpecialAgent, configure: a => a.Codename = "Owl"));
        db.Users.Add(Seed.Agent("klein", Rank.JuniorAgent, configure: a => a.Codename = "Wren"));
        await db.SaveChangesAsync();
        return ctx;
    }

    private static PublicLeadershipInput Input(string agentId = "chef", string? id = null) => new()
    {
        Id = id,
        AgentId = agentId,
        DisplayName = "Marcus Hale",
        Title = "Director",
        RoleText = "Leitung der Behörde",
        SortOrder = 10,
    };

    // ---- who may edit ----

    [Fact]
    public async Task APlainAgentMayNotEdit()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.SaveAsync(Input(), Agent()));
    }

    [Fact]
    public async Task TheReadOnlySupervisionMayNotEdit()
    {
        // the write guard runs before the rank guard, or the supervision gets as far as the roster check
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.SaveAsync(Input(), Supervision()));
    }

    // ---- who may appear ----

    [Fact]
    public async Task OnlyTheLeadershipBandMayAppear()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.SaveAsync(Input("klein"), Leader()));
    }

    [Fact]
    public async Task ASupervisorySpecialAgentIsTheFloor()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        var id = await host.Service.SaveAsync(Input("aufsicht2"), Leader());

        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public async Task AnUnknownAgentIsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.SaveAsync(Input("erfunden"), Leader()));
    }

    // ---- nothing goes live by itself ----

    [Fact]
    public async Task ASavedEntryIsNotPublicYet()
    {
        // switching the module on must publish nothing; every entry is released by hand
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SaveAsync(Input(), Leader());

        Assert.Empty(await host.Service.GetPublicAsync());
    }

    [Fact]
    public async Task AReleasedEntryIsPublicAndCarriesNoInternalHandle()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());

        await host.Service.PublishAsync(id, Leader());

        var card = Assert.Single(await host.Service.GetPublicAsync());
        Assert.Equal("Marcus Hale", card.DisplayName);
        Assert.Equal("Director", card.Title);
        // the outward handle is the public key, never the row id
        Assert.NotEqual(id, card.Key);
    }

    [Fact]
    public async Task WithTheModuleOffNothingIsPublicAndReleasingIsRefused()
    {
        using var ctx = await SeededAsync(moduleOn: false);
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, Leader()));
        Assert.Empty(await host.Service.GetPublicAsync());
    }

    [Fact]
    public async Task WithdrawingWorksEvenAfterTheModuleWasSwitchedOff()
    {
        // depublishing must never need a living module, or a mistake could not be taken back
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());
        await host.Service.PublishAsync(id, Leader());

        await using (var db = ctx.NewContext())
        {
            (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Leadership)).IsEnabled = false;
            await db.SaveChangesAsync();
        }

        await host.Service.RetractAsync(id, Leader());

        await using var check = ctx.NewContext();
        Assert.Null((await check.OeffentlicheFuehrungsprofile.SingleAsync(p => p.Id == id)).PublishedAt);
    }

    // ---- the photo ----

    [Fact]
    public async Task ThePhotoIsOnlyServedForAReleasedEntry()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());
        await host.Service.SetPhotoAsync(id, new MemoryStream([1, 2, 3]), "image/jpeg", Leader());

        var key = (await host.Service.GetAllAsync(Leader())).Single().Key;
        Assert.Null(await host.Service.GetPublishedPhotoAsync(key));

        await host.Service.PublishAsync(id, Leader());

        Assert.NotNull(await host.Service.GetPublishedPhotoAsync(key));
    }

    [Fact]
    public async Task AnUnknownKeyIsIndistinguishableFromAnUnreleasedOne()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        Assert.Null(await host.Service.GetPublishedPhotoAsync("gibtesnicht"));
    }

    [Fact]
    public async Task AForeignFileTypeIsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.SetPhotoAsync(id, new MemoryStream([1]), "application/pdf", Leader()));
    }

    // ---- the copy is editorial ----

    [Fact]
    public async Task ThePublishedValuesDoNotFollowTheRoster()
    {
        // the whole point of a snapshot: a rename or a promotion must not rewrite a released chart
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());
        await host.Service.PublishAsync(id, Leader());

        await using (var db = ctx.NewContext())
        {
            var agent = await db.Users.SingleAsync(u => u.Id == "chef");
            agent.Codename = "Phoenix";
            agent.RealName = "Ein ganz anderer Name";
            await db.SaveChangesAsync();
        }

        var card = Assert.Single(await host.Service.GetPublicAsync());
        Assert.Equal("Marcus Hale", card.DisplayName);
    }

    [Fact]
    public async Task DeletingRemovesTheEntryAndItsPhotoCopy()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveAsync(Input(), Leader());
        await host.Service.SetPhotoAsync(id, new MemoryStream([1, 2, 3]), "image/jpeg", Leader());

        await host.Service.DeleteAsync(id, Leader());

        await using var db = ctx.NewContext();
        Assert.Empty(await db.OeffentlicheFuehrungsprofile.ToListAsync());
        host.Storage.Received(1).Delete("kopie.jpg");
    }
}
