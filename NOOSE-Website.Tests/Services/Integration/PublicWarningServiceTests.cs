using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Public warnings: draft, publish, expire — and what the expiry filter is actually for.</summary>
public sealed class PublicWarningServiceTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal Senior()
        => ClaimsPrincipalBuilder.Agent("senior").WithRank(Rank.SeniorSpecialAgent).WithCodename("Kite").Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static ClaimsPrincipal Citizen()
        => ClaimsPrincipalBuilder.Agent("buerger").WithStatus(AgentStatus.Civilian).Build();

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    private sealed record Host(PublicWarningService Service, IMemoryCache Cache, TestDbContextFactory Factory);

    /// <summary>The service with the audit interceptor attached, as in production.</summary>
    /// <remarks>
    /// The interceptor is what rewrites a <c>Remove</c> into a soft delete, so the recycle-bin tests would exercise a
    /// hard delete without it.
    /// </remarks>
    private static Host NewHost(SqliteTestContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        var factory = new TestDbContextFactory(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var modules = new PublicModuleService(factory, cache);
        return new Host(new PublicWarningService(factory, modules, cache), cache, factory);
    }

    private static async Task<SqliteTestContext> SeededAsync(bool warningsOn = true)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Warnings)).IsEnabled = warningsOn;
        // distinct DiscordId: the column defaults to "" and carries a unique index
        db.Users.Add(new Agent
        {
            Id = "lead", UserName = "lead", DiscordId = "9001", Codename = "Falcon",
            Status = AgentStatus.Active, Rank = Rank.Director,
        });
        await db.SaveChangesAsync();
        return ctx;
    }

    /// <summary>Flips the module and drops the 10 s snapshot so the change is visible now.</summary>
    private static async Task ModuleAsync(SqliteTestContext ctx, Host host, bool on)
    {
        await using var db = ctx.NewContext();
        (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Warnings)).IsEnabled = on;
        await db.SaveChangesAsync();
        host.Cache.Remove("OeffentlicheModule");
    }

    private static WarningInput Draft(string title = "Sperrung Innenstadt", string html = "<p>Meiden Sie das Gebiet.</p>",
        DateTime? until = null)
        => new() { Title = title, DraftHtml = html, ValidUntil = until };

    private static async Task<string> PublishedAsync(Host host, DateTime? until = null)
    {
        var id = await host.Service.SaveDraftAsync(Draft(until: until), Leader());
        await host.Service.PublishAsync(id, Leader());
        return id;
    }

    /// <summary>Backdates the expiry past the service, which refuses to publish one that is already over.</summary>
    private static async Task ExpireAsync(SqliteTestContext ctx, Host host, string id, DateTime until)
    {
        await using var db = ctx.NewContext();
        (await db.OeffentlicheWarnungen.SingleAsync(w => w.Id == id)).ValidUntil = until;
        await db.SaveChangesAsync();
        host.Cache.Remove("OeffentlicheWarnungen");
    }

    // ---- drafts stay inside ----

    [Fact]
    public async Task ADraft_IsNotReachableAnonymously()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await host.Service.SaveDraftAsync(Draft(), Leader());

        Assert.Empty((await host.Service.GetPublishedAsync()).Cards);
    }

    [Fact]
    public async Task Publishing_PutsTitleAndBodyOutside()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await PublishedAsync(host);

        var card = Assert.Single((await host.Service.GetPublishedAsync()).Cards);
        Assert.Equal("Sperrung Innenstadt", card.Title);
        Assert.Contains("Meiden Sie das Gebiet", card.Html);
    }

    [Fact]
    public async Task SavingADraft_LeavesThePublishedCopyAlone()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await host.Service.SaveDraftAsync(
            new WarningInput { Id = id, Title = "Ganz anderer Titel", DraftHtml = "<p>Neuer Text.</p>" }, Leader());

        var card = Assert.Single((await host.Service.GetPublishedAsync()).Cards);
        Assert.Equal("Sperrung Innenstadt", card.Title);
        Assert.Contains("Meiden Sie das Gebiet", card.Html);
    }

    [Fact]
    public async Task ADivergingDraft_IsFlaggedInThePanel()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        Assert.False((await host.Service.GetAllAsync(Leader())).Single(r => r.Id == id).DraftDiffers);

        await host.Service.SaveDraftAsync(new WarningInput { Id = id, Title = "Korrigiert" }, Leader());

        Assert.True((await host.Service.GetAllAsync(Leader())).Single(r => r.Id == id).DraftDiffers);
    }

    [Fact]
    public async Task ANullDraftBody_LeavesTheStoredTextAlone()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(Draft(), Leader());

        await host.Service.SaveDraftAsync(new WarningInput { Id = id, Title = "Nur der Titel" }, Leader());

        var draft = await host.Service.GetDraftAsync(id, Leader());
        Assert.Contains("Meiden Sie das Gebiet", draft!.Html);
    }

    [Fact]
    public async Task AnEmptyDraftBody_ClearsTheStoredText()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(Draft(), Leader());

        await host.Service.SaveDraftAsync(new WarningInput { Id = id, Title = "Leer", DraftHtml = "" }, Leader());

        var draft = await host.Service.GetDraftAsync(id, Leader());
        Assert.Equal(string.Empty, draft!.Html);
    }

    [Fact]
    public async Task AnEmptyDraft_IsNotPublishable()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(Draft(html: "<p>   </p>"), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, Leader()));
    }

    [Fact]
    public async Task AWarningThatIsOnlyAPicture_Counts()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(
            Draft(html: "<p><img src=\"data:image/png;base64,iVBORw0KGgo=\" /></p>"), Leader());

        await host.Service.PublishAsync(id, Leader());

        Assert.Single((await host.Service.GetPublishedAsync()).Cards);
    }

    [Fact]
    public async Task PublishingCleansTheMarkup()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(Draft(html: "<p>Text</p><script>alert(1)</script>"), Leader());

        await host.Service.PublishAsync(id, Leader());

        var card = Assert.Single((await host.Service.GetPublishedAsync()).Cards);
        Assert.DoesNotContain("<script", card.Html, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the expiry is the control ----

    [Fact]
    public async Task AnExpiredWarning_FallsOutOfTheReadPath()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host, DateTime.UtcNow.AddDays(1));
        Assert.Single((await host.Service.GetPublishedAsync()).Cards);

        await ExpireAsync(ctx, host, id, DateTime.UtcNow.AddMinutes(-1));

        // no worker involved: the filter alone takes it off the page
        Assert.Empty((await host.Service.GetPublishedAsync()).Cards);
    }

    [Fact]
    public async Task AnExpiredWarning_KeepsItsPublishedStatusAndSaysSoInside()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host, DateTime.UtcNow.AddDays(1));

        await ExpireAsync(ctx, host, id, DateTime.UtcNow.AddMinutes(-1));

        var row = (await host.Service.GetAllAsync(Leader())).Single(r => r.Id == id);
        Assert.Equal(PublicWarningStatus.Veroeffentlicht, row.Status);
        Assert.True(row.IsExpired);
    }

    [Fact]
    public async Task AWarningWithoutAnExpiry_StandsUntilItIsRetracted()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await PublishedAsync(host);

        var card = Assert.Single((await host.Service.GetPublishedAsync()).Cards);
        Assert.Null(card.ValidUntil);
    }

    [Fact]
    public async Task AnExpiryInThePast_IsRefusedRatherThanPublishedIntoNothing()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(Draft(until: DateTime.UtcNow.AddDays(-2)), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, Leader()));
    }

    [Fact]
    public async Task APickedDay_CountsThroughItsEnd()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var today = DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Local);

        var id = await host.Service.SaveDraftAsync(Draft(until: today), Leader());
        await host.Service.PublishAsync(id, Leader());

        // picking today must not expire the warning at midnight this morning
        Assert.Single((await host.Service.GetPublishedAsync()).Cards);
    }

    // ---- retract, delete, restore ----

    [Fact]
    public async Task Retracting_TakesItOffAndKeepsTheBody()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await host.Service.RetractAsync(id, Leader());

        Assert.Empty((await host.Service.GetPublishedAsync()).Cards);
        await using var db = ctx.NewContext();
        var row = await db.OeffentlicheWarnungen.SingleAsync(w => w.Id == id);
        Assert.Contains("Meiden Sie das Gebiet", row.ContentHtml);
    }

    [Fact]
    public async Task Retracting_WorksWhileTheModuleIsOff()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await ModuleAsync(ctx, host, false);

        await host.Service.RetractAsync(id, Leader());

        await ModuleAsync(ctx, host, true);
        Assert.Empty((await host.Service.GetPublishedAsync()).Cards);
    }

    [Fact]
    public async Task DeletingAPublishedWarning_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.DeleteAsync(id, Leader()));
    }

    [Fact]
    public async Task ARestoredWarning_ComesBackAsADraft()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await host.Service.RetractAsync(id, Leader());
        await host.Service.DeleteAsync(id, Leader());

        Assert.Single(await host.Service.GetTrashAsync());
        await host.Service.RestoreAsync(id, Leader());

        var row = (await host.Service.GetAllAsync(Leader())).Single(r => r.Id == id);
        Assert.Equal(PublicWarningStatus.Entwurf, row.Status);
        Assert.Empty((await host.Service.GetPublishedAsync()).Cards);
    }

    // ---- module ----

    [Fact]
    public async Task TheModuleBeingOff_HidesEveryWarning()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);

        await ModuleAsync(ctx, host, false);

        Assert.Empty((await host.Service.GetPublishedAsync()).Cards);
    }

    [Fact]
    public async Task PublishingWithTheModuleOff_IsRefused()
    {
        using var ctx = await SeededAsync(warningsOn: false);
        var host = NewHost(ctx);
        var id = await host.Service.SaveDraftAsync(Draft(), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, Leader()));
    }

    // ---- guards ----

    [Fact]
    public async Task TheReadOnlySupervision_ReadsButDoesNotWrite()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);

        Assert.Single(await host.Service.GetAllAsync(OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.SaveDraftAsync(Draft(), OnlyReader()));
    }

    [Fact]
    public async Task ARankThreeAgent_MayNotWriteAWarning()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.SaveDraftAsync(Draft(), Senior()));
    }

    [Fact]
    public async Task ARankThreeAgent_DoesNotEvenReadThePanel()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        // the panel sits behind LeadershipPage, and the service says the same thing
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAllAsync(Senior()));
    }

    [Fact]
    public async Task ASignedInCitizen_IsOutOnBothSides()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAllAsync(Citizen()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.SaveDraftAsync(Draft(), Citizen()));
    }

    // ---- the panel row ----

    [Fact]
    public async Task ThePanelRow_NamesThePublisherByCodename()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        var row = (await host.Service.GetAllAsync(Leader())).Single(r => r.Id == id);
        Assert.Equal("Falcon", row.PublishedByName);
    }
}
