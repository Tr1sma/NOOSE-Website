using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="PublicFaqService"/>: the two gates, the visibility switches, the anchor.</summary>
public sealed class PublicFaqServiceTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal PlainAgent()
        => ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.SpecialAgent).Build();

    /// <summary>Read-only supervision: leadership rank, no admin flag, team-lead marker.</summary>
    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    private static PublicFaqService NewService(SqliteTestContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        var factory = new TestDbContextFactory(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new PublicFaqService(factory, new PublicModuleService(factory, cache), cache);
    }

    /// <summary>Module switches seeded, the FAQ module on, and the editorial row it owns published.</summary>
    private static async Task<SqliteTestContext> SeededAsync(bool moduleOn = true, bool pagePublished = true)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        if (moduleOn)
        {
            (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Faq)).IsEnabled = true;
        }
        db.OeffentlicheSeiten.Add(new OeffentlicheSeite
        {
            Slug = PublicFaq.PageSlug,
            Title = "Häufige Fragen",
            ContentHtml = "<p>Einleitung</p>",
            DraftHtml = "<p>Einleitung</p>",
            Status = pagePublished ? PublicPageStatus.Veroeffentlicht : PublicPageStatus.Entwurf,
        });
        await db.SaveChangesAsync();
        return ctx;
    }

    private static PublicFaqRubrikInput Rubrik(string title = "Hinweise", bool visible = true, bool open = false)
        => new() { Title = title, IsVisible = visible, DefaultOpen = open };

    private static PublicFaqEntryInput Entry(
        string rubrikId, string question = "Wie gebe ich einen Hinweis?", string? html = "<p>Antwort</p>",
        bool visible = true)
        => new() { RubrikId = rubrikId, Question = question, AnswerHtml = html, IsVisible = visible };

    // ---- the two gates ----

    [Fact]
    public async Task ThePublishedSnapshot_CarriesTheVisibleQuestions()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId), Leader());

        var snapshot = await service.GetPublishedAsync();

        var rubrik = Assert.Single(snapshot.Rubriken);
        Assert.Equal("Hinweise", rubrik.Title);
        var entry = Assert.Single(rubrik.Entries);
        Assert.Equal("Wie gebe ich einen Hinweis?", entry.Question);
        Assert.Equal("wie-gebe-ich-einen-hinweis", entry.Anchor);
    }

    [Fact]
    public async Task WithTheModuleOff_NothingIsPublished()
    {
        using var ctx = await SeededAsync(moduleOn: false);
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId), Leader());

        Assert.True((await service.GetPublishedAsync()).IsEmpty);
    }

    [Fact]
    public async Task WithTheFaqPageUnpublished_NothingIsPublished()
    {
        // the questions live on that page: a retracted page must not leave findable, unreachable answers behind
        using var ctx = await SeededAsync(pagePublished: false);
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId), Leader());

        Assert.True((await service.GetPublishedAsync()).IsEmpty);
    }

    [Fact]
    public async Task TheInformationModule_NoLongerGatesTheFaq()
    {
        // the FAQ left /info for a page and a switch of its own; closing the editorial pages must not close it
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.InfoPages)).IsEnabled = false;
            await db.SaveChangesAsync();
        }
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId), Leader());

        Assert.False((await service.GetPublishedAsync()).IsEmpty);
    }

    // ---- the page head ----

    [Fact]
    public async Task ThePublishedSnapshot_CarriesTheHeadingAndIntroOfItsOwnPage()
    {
        // /faq renders from this one service: the Information module must not be able to blank the text over a
        // FAQ that is switched on, so the head travels with the sections instead of through the page service
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var head = (await service.GetPublishedAsync()).Page;

        Assert.NotNull(head);
        Assert.Equal("Häufige Fragen", head!.Title);
        Assert.Equal("<p>Einleitung</p>", head.Html);
        Assert.False(head.IsDraft);
    }

    [Fact]
    public async Task TheHeading_SurvivesAFaqWithoutASingleSection()
    {
        // the page is published, it just has nothing under it yet - dropping the head would answer "not published"
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var snapshot = await service.GetPublishedAsync();

        Assert.True(snapshot.IsEmpty);
        Assert.NotNull(snapshot.Page);
    }

    [Fact]
    public async Task WithoutAPublishedPage_ThereIsNoHeadEither()
    {
        using var ctx = await SeededAsync(pagePublished: false);
        var service = NewService(ctx);

        Assert.Null((await service.GetPublishedAsync()).Page);
    }

    [Fact]
    public async Task ThePreview_CarriesTheDraftText_PastBothGates()
    {
        using var ctx = await SeededAsync(moduleOn: false, pagePublished: false);
        await using (var db = ctx.NewContext())
        {
            (await db.OeffentlicheSeiten.SingleAsync(p => p.Slug == PublicFaq.PageSlug)).DraftHtml = "<p>Entwurf</p>";
            await db.SaveChangesAsync();
        }
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(visible: false), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, visible: false), Leader());

        var snapshot = await service.GetPreviewAsync(Leader());

        Assert.Equal("<p>Entwurf</p>", snapshot.Page?.Html);
        Assert.True(snapshot.Page?.IsDraft);
        Assert.Single(snapshot.Rubriken);
    }

    // ---- visibility ----

    [Fact]
    public async Task AHiddenQuestion_StaysOutOfThePublishedSnapshot()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "Sichtbar"), Leader());
        var hidden = await service.SaveEntryAsync(Entry(rubrikId, "Versteckt"), Leader());
        await service.SetEntryVisibleAsync(hidden, false, Leader());

        var rubrik = Assert.Single((await service.GetPublishedAsync()).Rubriken);

        Assert.Equal("Sichtbar", Assert.Single(rubrik.Entries).Question);
    }

    [Fact]
    public async Task AHiddenRubrik_TakesItsQuestionsWithIt()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId), Leader());
        await service.SetRubrikVisibleAsync(rubrikId, false, Leader());

        Assert.True((await service.GetPublishedAsync()).IsEmpty);
    }

    [Fact]
    public async Task ARubrikWithoutAVisibleQuestion_IsNotAHeadingOverNothing()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveRubrikAsync(Rubrik(), Leader());

        Assert.True((await service.GetPublishedAsync()).IsEmpty);
    }

    [Fact]
    public async Task ThePreview_ShowsWhatIsSwitchedOff_AndMarksIt()
    {
        using var ctx = await SeededAsync(moduleOn: false, pagePublished: false);
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(visible: false), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, visible: false), Leader());

        var preview = await service.GetPreviewAsync(Leader());

        var rubrik = Assert.Single(preview.Rubriken);
        Assert.True(rubrik.Hidden);
        Assert.True(Assert.Single(rubrik.Entries).Hidden);
    }

    // ---- the anchor ----

    [Fact]
    public async Task TwoQuestionsWithTheSameWording_GetDistinctAnchors()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "Was kostet das?"), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "Was kostet das?"), Leader());

        var anchors = Assert.Single((await service.GetPublishedAsync()).Rubriken)
            .Entries.Select(e => e.Anchor).ToList();

        Assert.Equal(["was-kostet-das", "was-kostet-das-2"], anchors);
    }

    [Fact]
    public async Task RenamingAQuestion_LeavesItsAnchorAlone()
    {
        // an anchor that followed the wording would kill every link somebody had already shared
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        var id = await service.SaveEntryAsync(Entry(rubrikId, "Alte Frage"), Leader());

        await service.SaveEntryAsync(
            new PublicFaqEntryInput { Id = id, RubrikId = rubrikId, Question = "Ganz neue Frage" }, Leader());

        var entry = Assert.Single(Assert.Single((await service.GetPublishedAsync()).Rubriken).Entries);
        Assert.Equal("Ganz neue Frage", entry.Question);
        Assert.Equal("alte-frage", entry.Anchor);
    }

    [Fact]
    public async Task AQuestionWithoutUsableLetters_StillGetsAnAddress()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "?!?"), Leader());

        var entry = Assert.Single(Assert.Single((await service.GetPublishedAsync()).Rubriken).Entries);
        Assert.StartsWith("frage-", entry.Anchor);
        Assert.True(PublicPageSlug.IsValid(entry.Anchor));
    }

    // ---- content ----

    [Fact]
    public async Task TheAnswer_IsSanitisedOnSave()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(
            Entry(rubrikId, html: "<p>Hallo</p><script>alert(1)</script>"), Leader());

        var entry = Assert.Single(Assert.Single((await service.GetPublishedAsync()).Rubriken).Entries);
        Assert.DoesNotContain("script", entry.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hallo", entry.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingWithoutAnAnswer_LeavesTheStoredOneAlone()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        var id = await service.SaveEntryAsync(Entry(rubrikId, html: "<p>Bleibt stehen</p>"), Leader());

        await service.SaveEntryAsync(
            new PublicFaqEntryInput { Id = id, RubrikId = rubrikId, Question = "Umbenannt", AnswerHtml = null },
            Leader());

        Assert.Contains("Bleibt stehen", await service.GetAnswerAsync(id, Leader()) ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePlainTextOfAnAnswer_IsPrecomputedForTheSearch()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, html: "<p>Eine <b>Belohnung</b> ist möglich.</p>"), Leader());

        var entry = Assert.Single(Assert.Single((await service.GetPublishedAsync()).Rubriken).Entries);
        Assert.Contains("Belohnung", entry.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("<", entry.PlainText, StringComparison.Ordinal);
    }

    // ---- ordering ----

    [Fact]
    public async Task MovingAQuestionUp_ChangesTheOrderOfItsOwnRubrikOnly()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var first = await service.SaveRubrikAsync(Rubrik("Erste"), Leader());
        var second = await service.SaveRubrikAsync(Rubrik("Zweite"), Leader());
        await service.SaveEntryAsync(Entry(first, "A"), Leader());
        var b = await service.SaveEntryAsync(Entry(first, "B"), Leader());
        await service.SaveEntryAsync(Entry(second, "C"), Leader());

        await service.MoveEntryAsync(b, -1, Leader());

        var snapshot = await service.GetPublishedAsync();
        Assert.Equal(["B", "A"], snapshot.Rubriken[0].Entries.Select(e => e.Question));
        Assert.Equal(["C"], snapshot.Rubriken[1].Entries.Select(e => e.Question));
    }

    [Fact]
    public async Task MovingPastTheEnd_DoesNothing()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        var a = await service.SaveEntryAsync(Entry(rubrikId, "A"), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "B"), Leader());

        await service.MoveEntryAsync(a, -1, Leader());

        Assert.Equal(["A", "B"],
            Assert.Single((await service.GetPublishedAsync()).Rubriken).Entries.Select(e => e.Question));
    }

    [Fact]
    public async Task MovingARubrikDown_ReordersTheSections()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var first = await service.SaveRubrikAsync(Rubrik("Erste"), Leader());
        var second = await service.SaveRubrikAsync(Rubrik("Zweite"), Leader());
        await service.SaveEntryAsync(Entry(first, "A"), Leader());
        await service.SaveEntryAsync(Entry(second, "B"), Leader());

        await service.MoveRubrikAsync(first, 1, Leader());

        Assert.Equal(["Zweite", "Erste"],
            (await service.GetPublishedAsync()).Rubriken.Select(r => r.Title));
    }

    [Fact]
    public async Task AQuestionMovedToAnotherRubrik_LandsAtTheEndThere()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var first = await service.SaveRubrikAsync(Rubrik("Erste"), Leader());
        var second = await service.SaveRubrikAsync(Rubrik("Zweite"), Leader());
        var moved = await service.SaveEntryAsync(Entry(first, "Wandert"), Leader());
        await service.SaveEntryAsync(Entry(second, "Steht schon da"), Leader());

        await service.SaveEntryAsync(
            new PublicFaqEntryInput { Id = moved, RubrikId = second, Question = "Wandert" }, Leader());

        var snapshot = await service.GetPublishedAsync();
        Assert.Equal("Zweite", Assert.Single(snapshot.Rubriken).Title);
        Assert.Equal(["Steht schon da", "Wandert"], snapshot.Rubriken[0].Entries.Select(e => e.Question));
    }

    // ---- deleting ----

    [Fact]
    public async Task ARubrikWithQuestions_IsNotDeleted()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId), Leader());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteRubrikAsync(rubrikId, Leader()));

        Assert.Contains("Fragen", error.Message, StringComparison.Ordinal);
        Assert.Single((await service.GetAllAsync(Leader())).Rubriken);
    }

    [Fact]
    public async Task AnEmptyRubrik_IsDeleted()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());

        await service.DeleteRubrikAsync(rubrikId, Leader());

        Assert.Empty((await service.GetAllAsync(Leader())).Rubriken);
    }

    // ---- who may write ----

    [Theory]
    [InlineData("agent")]
    [InlineData("aufsicht")]
    public async Task OnlyLeadershipWithWriteAccess_MayEdit(string who)
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var actor = who == "agent" ? PlainAgent() : OnlyReader();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SaveRubrikAsync(Rubrik(), actor));
    }

    [Fact]
    public async Task TheReadOnlySupervision_StillSeesTheEditorialPanel()
    {
        // it must be able to read what the agency says publicly, it just may not be the one saying it
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.SaveRubrikAsync(Rubrik(), Leader());

        var view = await service.GetAllAsync(OnlyReader());

        Assert.Single(view.Rubriken);
        Assert.True(view.PageIsPublished);
        Assert.True(view.ModuleIsOn);
    }

    [Fact]
    public async Task ThePanel_ReportsTheTwoGatesSeparately()
    {
        using var ctx = await SeededAsync(moduleOn: false, pagePublished: false);
        var service = NewService(ctx);

        var view = await service.GetAllAsync(Leader());

        Assert.False(view.PageIsPublished);
        Assert.False(view.ModuleIsOn);
    }

    [Fact]
    public async Task ThePanelRow_CarriesNoAnswer_ButSaysWhetherThereIsOne()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var rubrikId = await service.SaveRubrikAsync(Rubrik(), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "Mit Antwort", "<p>Da</p>"), Leader());
        await service.SaveEntryAsync(Entry(rubrikId, "Ohne Antwort", null), Leader());

        var rows = Assert.Single((await service.GetAllAsync(Leader())).Rubriken).Entries;

        Assert.True(rows.Single(r => r.Question == "Mit Antwort").HasAnswer);
        Assert.False(rows.Single(r => r.Question == "Ohne Antwort").HasAnswer);
    }
}
