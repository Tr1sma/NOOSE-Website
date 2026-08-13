using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="PublicPageService"/>: who may publish, and what publishing actually exposes.</summary>
public sealed class PublicPageServiceTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal PlainAgent()
        => ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.SpecialAgent).Build();

    /// <summary>Read-only supervision: leadership rank, no admin flag, team-lead marker.</summary>
    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    /// <summary>Stub acting agent for the audit interceptor.</summary>
    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    /// <summary>The service with the audit interceptor attached, as in production.</summary>
    /// <remarks>
    /// The interceptor is what rewrites a <c>Remove</c> into a soft delete, so the recycle-bin tests would be
    /// exercising a hard delete without it. One cache for both services, the way the container hands it out.
    /// </remarks>
    private static PublicPageService NewService(SqliteTestContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        var factory = new TestDbContextFactory(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new PublicPageService(factory, new PublicModuleService(factory, cache), cache);
    }

    /// <summary>Seeds the module switches and turns the editorial module on unless asked otherwise.</summary>
    private static async Task<SqliteTestContext> SeededAsync(bool moduleOn = true)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        if (moduleOn)
        {
            var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.InfoPages);
            row.IsEnabled = true;
            await db.SaveChangesAsync();
        }
        return ctx;
    }

    private static PublicPageInput Input(
        string slug = "auftrag",
        string title = "Unser Auftrag",
        string? html = "<p>Text</p>",
        string? id = null,
        int sortOrder = 10,
        bool showInMenu = true,
        string? icon = null,
        string? menuTitle = null)
        => new()
        {
            Id = id,
            Slug = slug,
            Title = title,
            DraftHtml = html,
            SortOrder = sortOrder,
            ShowInMenu = showInMenu,
            IconName = icon,
            MenuTitle = menuTitle,
        };

    /// <summary>Creates a page and publishes it; returns its id.</summary>
    private static async Task<string> PublishedAsync(PublicPageService service, PublicPageInput input)
    {
        var id = await service.SaveDraftAsync(input, Leader());
        await service.PublishAsync(id, Leader());
        return id;
    }

    // ---- seeding ----

    [Fact]
    public async Task Seeder_CreatesTheStarterPagesAsDrafts()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
        }

        await using var read = ctx.NewContext();
        var rows = await read.OeffentlicheSeiten.ToListAsync();
        Assert.Equal(PublicPageSeeder.Starters.Count, rows.Count);
        Assert.All(rows, row => Assert.Equal(PublicPageStatus.Entwurf, row.Status));
        // a seeded page must never be online: nothing goes public by deploying
        Assert.All(rows, row => Assert.Null(row.ContentHtml));
    }

    [Fact]
    public async Task Seeder_IsIdempotent()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
        }
        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
        }

        await using var read = ctx.NewContext();
        Assert.Equal(PublicPageSeeder.Starters.Count, await read.OeffentlicheSeiten.CountAsync());
    }

    [Fact]
    public async Task Seeder_NeverOverwritesAnEditedPage()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
            var row = await db.OeffentlicheSeiten.SingleAsync(p => p.Slug == "faq");
            row.Title = "Fragen und Antworten";
            row.DraftHtml = "<p>Selbst geschrieben</p>";
            await db.SaveChangesAsync();
        }

        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
        }

        await using var read = ctx.NewContext();
        var faq = await read.OeffentlicheSeiten.SingleAsync(p => p.Slug == "faq");
        Assert.Equal("Fragen und Antworten", faq.Title);
        Assert.Equal("<p>Selbst geschrieben</p>", faq.DraftHtml);
    }

    [Fact]
    public async Task Seeder_DoesNotResurrectADeletedPage()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
            // the state a soft delete leaves behind; this context carries no interceptor, so set it directly
            (await db.OeffentlicheSeiten.SingleAsync(p => p.Slug == "faq")).IsDeleted = true;
            await db.SaveChangesAsync();
        }

        await using (var db = ctx.NewContext())
        {
            await PublicPageSeeder.SeedAsync(db);
        }

        await using var read = ctx.NewContext();
        Assert.Equal(PublicPageSeeder.Starters.Count - 1, await read.OeffentlicheSeiten.CountAsync());
        Assert.Equal(1, await read.OeffentlicheSeiten.IgnoreQueryFilters().CountAsync(p => p.Slug == "faq"));
    }

    // ---- reading from outside ----

    [Fact]
    public async Task GetAsync_ADraft_IsNotReachableAnonymously()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveDraftAsync(Input(), Leader());

        Assert.Null(await service.GetAsync("auftrag"));
        Assert.Empty(await service.GetMenuAsync());
    }

    [Fact]
    public async Task GetAsync_ReturnsThePublishedCopy()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input(html: "<p>Wir schützen die Ordnung.</p>"));

        var page = await service.GetAsync("auftrag");
        Assert.NotNull(page);
        Assert.Equal("Unser Auftrag", page.Title);
        Assert.Contains("Wir schützen die Ordnung.", page.Html, StringComparison.Ordinal);
        Assert.False(page.IsDraft);
        Assert.NotNull(page.PublishedAt);
    }

    [Fact]
    public async Task GetAsync_MatchesTheSlugCaseInsensitively()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input());

        Assert.NotNull(await service.GetAsync("Auftrag"));
        Assert.NotNull(await service.GetAsync("AUFTRAG"));
    }

    [Fact]
    public async Task GetAsync_UnknownSlug_IsNull()
    {
        using var ctx = await SeededAsync();
        Assert.Null(await NewService(ctx).GetAsync("gibtsnicht"));
    }

    [Fact]
    public async Task GetAsync_WhileTheModuleIsOff_ShowsNothing()
    {
        using var ctx = await SeededAsync(moduleOn: false);
        var service = NewService(ctx);

        await PublishedAsync(service, Input());

        // the switch is enforced in the service, not only by the page gate
        Assert.Null(await service.GetAsync("auftrag"));
        Assert.Empty(await service.GetMenuAsync());
    }

    [Fact]
    public async Task GetMenuAsync_OrdersBySortOrderThenTitle()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input("faq", "Häufige Fragen", sortOrder: 40));
        await PublishedAsync(service, Input("befugnisse", "Befugnisse", sortOrder: 20));
        await PublishedAsync(service, Input("auftrag", "Auftrag", sortOrder: 20));

        var menu = await service.GetMenuAsync();
        Assert.Equal(["auftrag", "befugnisse", "faq"], menu.Select(m => m.Slug));
    }

    [Fact]
    public async Task GetMenuAsync_UsesTheMenuTitle_AndFallsBackToTheTitle()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input("faq", "Häufige Fragen", sortOrder: 10, menuTitle: "FAQ"));
        await PublishedAsync(service, Input("auftrag", "Auftrag", sortOrder: 20));

        var menu = await service.GetMenuAsync();
        Assert.Equal(["FAQ", "Auftrag"], menu.Select(m => m.MenuTitle));
    }

    [Fact]
    public async Task GetMenuAsync_ResolvesTheIcon_AndFallsBackToTheDefault()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input("recht", "Recht", sortOrder: 10, icon: "Gavel"));
        await PublishedAsync(service, Input("auftrag", "Auftrag", sortOrder: 20));

        var menu = await service.GetMenuAsync();
        Assert.Equal(PublicModules.IconFor("Gavel", "x"), menu[0].Icon);
        Assert.Equal(PublicModules.PageDefaultIcon, menu[1].Icon);
    }

    [Fact]
    public async Task AnUnlistedPage_StaysReachableByLink_ButIsNotInTheMenu()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input("nutzungshinweise", "Nutzungshinweise", showInMenu: false));

        Assert.Empty(await service.GetMenuAsync());
        Assert.NotNull(await service.GetAsync("nutzungshinweise"));
    }

    // ---- draft isolation, the point of the phase ----

    [Fact]
    public async Task SaveDraftAsync_DoesNotChangeWhatVisitorsRead()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input(html: "<p>Veröffentlicht</p>"));

        await service.SaveDraftAsync(Input(id: id, html: "<p>Noch nicht fertig</p>"), Leader());

        var page = await service.GetAsync("auftrag");
        Assert.NotNull(page);
        Assert.Contains("Veröffentlicht", page.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Noch nicht fertig", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_CopiesTheDraftOntoThePublishedVersion()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input(html: "<p>Erste Fassung</p>"));

        await service.SaveDraftAsync(Input(id: id, html: "<p>Zweite Fassung</p>"), Leader());
        await service.PublishAsync(id, Leader());

        var page = await service.GetAsync("auftrag");
        Assert.NotNull(page);
        Assert.Contains("Zweite Fassung", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_RecordsWhoPublishedAndWhen()
    {
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent("lead", Rank.Director, configure: a => a.Codename = "Falcon"));
            await db.SaveChangesAsync();
        }
        var service = NewService(ctx);

        await PublishedAsync(service, Input());

        var row = (await service.GetAllAsync(Leader())).Single();
        Assert.Equal(PublicPageStatus.Veroeffentlicht, row.Status);
        Assert.Equal("Falcon", row.PublishedByName);
        Assert.NotNull(row.PublishedAt);
        Assert.False(row.DraftDiffers);
    }

    [Fact]
    public async Task GetAllAsync_MarksADraftThatDiffersFromWhatIsOnline()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input(html: "<p>Online</p>"));

        await service.SaveDraftAsync(Input(id: id, html: "<p>Neu</p>"), Leader());

        Assert.True((await service.GetAllAsync(Leader())).Single().DraftDiffers);
    }

    // ---- preview ----

    [Fact]
    public async Task GetPreviewAsync_ShowsTheDraft_MarkedAsSuch()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveDraftAsync(Input(html: "<p>Noch Entwurf</p>"), Leader());

        var page = await service.GetPreviewAsync("auftrag", Leader());

        Assert.NotNull(page);
        Assert.True(page.IsDraft);
        Assert.Contains("Noch Entwurf", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPreviewAsync_WorksWhileTheModuleIsStillOff()
    {
        // preparing content before the module goes live is the whole reason a draft exists
        using var ctx = await SeededAsync(moduleOn: false);
        var service = NewService(ctx);
        await service.SaveDraftAsync(Input(), Leader());

        Assert.NotNull(await service.GetPreviewAsync("auftrag", Leader()));
    }

    [Fact]
    public async Task GetPreviewAsync_AdmitsTheReadOnlySupervision()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveDraftAsync(Input(), Leader());

        Assert.NotNull(await service.GetPreviewAsync("auftrag", OnlyReader()));
    }

    [Fact]
    public async Task GetPreviewAsync_PlainAgent_IsRefused()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveDraftAsync(Input(), Leader());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetPreviewAsync("auftrag", PlainAgent()));
    }

    [Fact]
    public async Task GetPreviewAsync_UnknownSlug_IsNull()
    {
        using var ctx = await SeededAsync();
        Assert.Null(await NewService(ctx).GetPreviewAsync("gibtsnicht", Leader()));
    }

    // ---- guards ----

    [Fact]
    public async Task GetAllAsync_AdmitsTheReadOnlySupervision_ButNotAPlainAgent()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        Assert.Empty(await service.GetAllAsync(OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync(PlainAgent()));
    }

    [Fact]
    public async Task Writing_IsRefusedForAPlainAgent()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SaveDraftAsync(Input(), PlainAgent()));
    }

    [Fact]
    public async Task Writing_IsRefusedForTheReadOnlySupervision()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SaveDraftAsync(Input(id: id), OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PublishAsync(id, OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetractAsync(id, OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(id, OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RestoreAsync(id, OnlyReader()));
    }

    // ---- validation ----

    [Fact]
    public async Task SaveDraftAsync_RejectsAnEmptyTitle()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveDraftAsync(Input(title: "   "), Leader()));
    }

    [Fact]
    public async Task SaveDraftAsync_RejectsAnUnusableAddress()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveDraftAsync(Input(slug: "!!!"), Leader()));
    }

    [Fact]
    public async Task SaveDraftAsync_FoldsAGermanAddressIntoASlug()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await PublishedAsync(service, Input(slug: "Über Uns", title: "Über uns"));

        Assert.NotNull(await service.GetAsync("ueber-uns"));
    }

    [Fact]
    public async Task SaveDraftAsync_RejectsAnAddressThatIsAlreadyTaken()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveDraftAsync(Input(), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveDraftAsync(Input(title: "Zweite Seite"), Leader()));
    }

    [Fact]
    public async Task SaveDraftAsync_KeepingItsOwnAddress_IsNotADuplicate()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(), Leader());

        await service.SaveDraftAsync(Input(id: id, title: "Auftrag, neu betitelt"), Leader());

        Assert.Equal("Auftrag, neu betitelt", (await service.GetAllAsync(Leader())).Single().Title);
    }

    [Fact]
    public async Task SaveDraftAsync_MayReuseTheAddressOfADeletedPage()
    {
        // the slug index is deliberately not unique; a soft-deleted row must not block the address forever
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(), Leader());
        await service.DeleteAsync(id, Leader());

        var second = await service.SaveDraftAsync(Input(title: "Auftrag, zweiter Versuch"), Leader());

        Assert.NotEqual(id, second);
    }

    [Fact]
    public async Task SaveDraftAsync_DropsAnIconThatIsNotOnTheAllowlist()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var id = await service.SaveDraftAsync(Input(icon: "<script>alert(1)</script>"), Leader());

        await using var db = ctx.NewContext();
        Assert.Null((await db.OeffentlicheSeiten.SingleAsync(p => p.Id == id)).IconName);
    }

    [Fact]
    public async Task SaveDraftAsync_SanitizesTheDraft()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var id = await service.SaveDraftAsync(
            Input(html: "<p>Text</p><script>alert(1)</script>"), Leader());

        await using var db = ctx.NewContext();
        var draft = (await db.OeffentlicheSeiten.SingleAsync(p => p.Id == id)).DraftHtml;
        Assert.NotNull(draft);
        Assert.DoesNotContain("<script", draft, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Text", draft, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_SanitizesAgain_EvenIfTheStoredDraftWasTampered()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(), Leader());

        await using (var db = ctx.NewContext())
        {
            // a raw write bypasses SaveDraftAsync; publishing is the moment the HTML becomes anonymous, so it cleans
            var row = await db.OeffentlicheSeiten.SingleAsync(p => p.Id == id);
            row.DraftHtml = "<p>Text</p><script>alert(1)</script>";
            await db.SaveChangesAsync();
        }

        await service.PublishAsync(id, Leader());

        var page = await service.GetAsync("auftrag");
        Assert.NotNull(page);
        Assert.DoesNotContain("<script", page.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveDraftAsync_WithoutABody_KeepsTheStoredDraft()
    {
        // a caller that only renames a page must not wipe its text, and the loss would be silent
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(html: "<p>Bestehender Text</p>"), Leader());

        await service.SaveDraftAsync(Input(id: id, title: "Neuer Titel", html: null), Leader());

        var row = (await service.GetAllAsync(Leader())).Single();
        Assert.Equal("Neuer Titel", row.Title);
        Assert.Contains("Bestehender Text", await service.GetDraftAsync(id, Leader()) ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveDraftAsync_WithAnEmptyBody_ClearsTheDraft()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(html: "<p>Bestehender Text</p>"), Leader());

        await service.SaveDraftAsync(Input(id: id, html: string.Empty), Leader());

        Assert.Equal(string.Empty, await service.GetDraftAsync(id, Leader()));
    }

    [Fact]
    public async Task GetDraftAsync_IsSeparateFromTheList_SoAListRowCarriesNoHtml()
    {
        // an editorial page holds its pictures as base64 in the body; a list of those would be megabytes per render
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(html: "<p>Der Text</p>"), Leader());

        Assert.Contains("Der Text", await service.GetDraftAsync(id, Leader()) ?? "", StringComparison.Ordinal);
        Assert.Null(await service.GetDraftAsync("gibtsnicht", Leader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDraftAsync(id, PlainAgent()));
    }

    [Fact]
    public async Task PublishAsync_AllowsAPageThatIsOnlyAPicture()
    {
        // a plain-text probe alone would call an image-only page an empty draft
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(
            Input(html: "<p><img src=\"data:image/png;base64,iVBORw0KGgo=\" alt=\"Organigramm\" /></p>"), Leader());

        await service.PublishAsync(id, Leader());

        var page = await service.GetAsync("auftrag");
        Assert.NotNull(page);
        Assert.Contains("<img", page.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_RefusesAnEmptyDraft()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(html: "<p>   </p>"), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(id, Leader()));
        Assert.Null(await service.GetAsync("auftrag"));
    }

    [Fact]
    public async Task SaveDraftAsync_ClampsTheSortOrder()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var id = await service.SaveDraftAsync(Input(sortOrder: 999_999), Leader());

        await using var db = ctx.NewContext();
        Assert.Equal(9999, (await db.OeffentlicheSeiten.SingleAsync(p => p.Id == id)).SortOrder);
    }

    [Fact]
    public async Task SaveDraftAsync_UnknownId_IsRejected()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveDraftAsync(Input(id: "gibtsnicht"), Leader()));
    }

    // ---- retract, delete, restore ----

    [Fact]
    public async Task RetractAsync_TakesThePageOffline_ButKeepsTheDraft()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input(html: "<p>Online</p>"));

        await service.RetractAsync(id, Leader());

        Assert.Null(await service.GetAsync("auftrag"));
        var page = await service.GetPreviewAsync("auftrag", Leader());
        Assert.NotNull(page);
        Assert.Contains("Online", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetractAsync_ThenPublishAgain_PutsThePageBack()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input());

        await service.RetractAsync(id, Leader());
        await service.PublishAsync(id, Leader());

        Assert.NotNull(await service.GetAsync("auftrag"));
    }

    [Fact]
    public async Task DeleteAsync_TakesThePageOffline_AndIntoTheTrash()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input());

        await service.DeleteAsync(id, Leader());

        Assert.Null(await service.GetAsync("auftrag"));
        Assert.Empty(await service.GetAllAsync(Leader()));
        var trash = await service.GetTrashAsync();
        Assert.Equal(id, Assert.Single(trash).Id);
    }

    [Fact]
    public async Task GetTrashAsync_ListsOnlyDeletedRows()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveDraftAsync(Input(), Leader());
        var doomed = await service.SaveDraftAsync(Input(slug: "faq", title: "FAQ"), Leader());

        await service.DeleteAsync(doomed, Leader());

        Assert.Equal(doomed, Assert.Single(await service.GetTrashAsync()).Id);
    }

    [Fact]
    public async Task RestoreAsync_BringsThePageBackAsADraft()
    {
        // undoing a delete must not put the page back online as a side effect
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input());
        await service.DeleteAsync(id, Leader());

        await service.RestoreAsync(id, Leader());

        Assert.Empty(await service.GetTrashAsync());
        Assert.Null(await service.GetAsync("auftrag"));
        Assert.Equal(PublicPageStatus.Entwurf, (await service.GetAllAsync(Leader())).Single().Status);
    }

    [Fact]
    public async Task RestoreAsync_WhenTheAddressWasTakenMeanwhile_IsRejected()
    {
        // reusing the address of a deleted page is allowed, so restoring the old one would leave two live pages on
        // one address — and an ambiguous /info would take the whole area down, not just this page
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var first = await service.SaveDraftAsync(Input(), Leader());
        await service.DeleteAsync(first, Leader());
        await service.SaveDraftAsync(Input(title: "Auftrag, neu"), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(first, Leader()));

        Assert.Single(await service.GetAllAsync(Leader()));
    }

    [Fact]
    public async Task TwoLivePagesOnOneAddress_DoNotBlankTheWholeArea()
    {
        // belt and braces: the slug carries no unique index, so a duplicate that got in some other way must cost at
        // most the ambiguous page, never every published page
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await PublishedAsync(service, Input(slug: "auftrag", title: "Auftrag", sortOrder: 10));
        await PublishedAsync(service, Input(slug: "faq", title: "FAQ", sortOrder: 20));

        await using (var db = ctx.NewContext())
        {
            // straight past the service's uniqueness check
            (await db.OeffentlicheSeiten.SingleAsync(p => p.Slug == "faq")).Slug = "auftrag";
            await db.SaveChangesAsync();
        }

        Assert.NotNull(await service.GetAsync("auftrag"));
        Assert.Single(await service.GetMenuAsync());
    }

    [Fact]
    public async Task RestoreAsync_APageThatIsNotDeleted_IsRejected()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(id, Leader()));
    }

    [Fact]
    public async Task TrashProjection_NamesTheAddressAndTheState()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await service.SaveDraftAsync(Input(), Leader());
        await service.DeleteAsync(id, Leader());

        var row = TrashProjection.PublicPage(Assert.Single(await service.GetTrashAsync()));

        Assert.Equal("oeffentliche-seiten", row.Kind);
        Assert.Equal("Unser Auftrag", row.Title);
        Assert.Contains("/info/auftrag", row.Detail);
    }

    // ---- caching ----

    [Fact]
    public async Task GetAsync_IsCached_AndDroppedOnAWrite()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var id = await PublishedAsync(service, Input(title: "Erster Titel"));

        // warm the snapshot first; publishing dropped it, so an immediate read would just reload
        Assert.Equal("Erster Titel", (await service.GetAsync("auftrag"))!.Title);

        await using (var db = ctx.NewContext())
        {
            // straight past the service, so only the cache can explain the stale answer
            var row = await db.OeffentlicheSeiten.SingleAsync(p => p.Id == id);
            row.Title = "Am Cache vorbei";
            await db.SaveChangesAsync();
        }
        Assert.Equal("Erster Titel", (await service.GetAsync("auftrag"))!.Title);

        await service.RetractAsync(id, Leader());
        Assert.Null(await service.GetAsync("auftrag"));
    }

    // ---- audit ----

    [Fact]
    public async Task PublishAndRetract_AreAuditedByTheInterceptor_WithoutAManualRow()
    {
        // OeffentlicheSeite is IAuditable, so the point is that the service writes no ManualAudit row of its own
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var id = await service.SaveDraftAsync(Input(), Leader());
        await service.PublishAsync(id, Leader());
        await service.RetractAsync(id, Leader());

        await using var read = ctx.NewContext();
        var rows = await read.AuditLogs
            .Where(a => a.EntityType == nameof(OeffentlicheSeite))
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(AuditAction.Created, rows[0].Action);
        Assert.Equal(AuditAction.Modified, rows[1].Action);
        Assert.Equal(AuditAction.Modified, rows[2].Action);
        // and the row is readable in /nachweis rather than showing a raw CLR name
        Assert.Equal("Öffentliche Seite", AuditEntityDisplay.Label(nameof(OeffentlicheSeite)));
    }
}
