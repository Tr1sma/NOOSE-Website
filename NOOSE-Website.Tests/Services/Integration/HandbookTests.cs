using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Infrastructure.Handbook;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Handbook;
using NOOSE_Website.Navigation;
using NOOSE_Website.Services.Handbook;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The handbook: the seeder's promise never to overwrite an edit, and who may write it.</summary>
public sealed class HandbookTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    /// <summary>The point of the guard: HRB is rank-independent, so a Junior Agent carrying it may write.</summary>
    private static ClaimsPrincipal JuniorHrb()
        => ClaimsPrincipalBuilder.Agent("hrb").WithRank(Rank.JuniorAgent).WithCodename("Sparrow").AsHrb().Build();

    private static ClaimsPrincipal Agent()
        => ClaimsPrincipalBuilder.Agent("agent").WithRank(Rank.SpecialAgent).WithCodename("Wren").Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    private static DbContextOptions<AppDbContext> Intercepted(SqliteTestContext ctx)
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;

    /// <summary>The service with the audit interceptor attached, as in production.</summary>
    /// <remarks>
    /// The interceptor is what rewrites a <c>Remove</c> into a soft delete; without it the recycle-bin tests would
    /// exercise a hard delete and the seeder's "do not revive" rule would pass without being tested at all.
    /// </remarks>
    private static HandbookService NewService(SqliteTestContext ctx)
        => new(new TestDbContextFactory(Intercepted(ctx)));

    private static async Task SeedAsync(
        SqliteTestContext ctx,
        IReadOnlyList<HandbookContent.SeededChapter> chapters,
        IReadOnlyList<HandbookContent.SeededTerm> terms,
        int revision)
    {
        await using var db = new AppDbContext(Intercepted(ctx));
        await HandbookSeeder.SeedAsync(db, chapters, terms, revision);
    }

    private static HandbookContent.SeededChapter[] OneChapter(
        string articleTitle = "Anmelden",
        string? stepTitle = "Mit Discord anmelden")
        =>
        [
            new("kap-a", "erste-schritte", "Erste Schritte", "Wie du anfängst.", "Start",
            [
                new("art-a", "anmelden", articleTitle, "Wie du hereinkommst.",
                    "<p>Die Anmeldung läuft über Discord.</p>",
                    RoleplayHtml: "<p>Das Konto gehört deinem Discord-Konto.</p>",
                    DiagramKey: "oberflaeche",
                    NavKey: "dashboard",
                    Steps: stepTitle is null ? null : [new("Login", stepTitle, "Auf den Knopf klicken.")]),
            ]),
        ];

    private static readonly HandbookContent.SeededTerm[] OneTerm =
        [new("beg-a", "Codename", "Der Name, unter dem dich alle sehen.", Synonyms: "Deckname", ArticleKey: "art-a")];

    // --- the seeder -------------------------------------------------------

    [Fact]
    public async Task Seeding_twice_creates_each_row_once()
    {
        using var ctx = new SqliteTestContext();

        await SeedAsync(ctx, OneChapter(), OneTerm, 1);
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        await using var check = ctx.NewContext();
        Assert.Equal(1, await check.HandbuchKapitel.CountAsync());
        Assert.Equal(1, await check.HandbuchArtikel.CountAsync());
        Assert.Equal(1, await check.HandbuchBegriffe.CountAsync());
        Assert.Equal(1, await check.HandbuchSchritte.CountAsync());
    }

    [Fact]
    public async Task An_untouched_article_follows_a_new_revision()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter("Alter Titel"), OneTerm, 1);

        await SeedAsync(ctx, OneChapter("Neuer Titel"), OneTerm, 2);

        await using var check = ctx.NewContext();
        var row = await check.HandbuchArtikel.SingleAsync(a => a.SeedKey == "art-a");
        Assert.Equal("Neuer Titel", row.Title);
        Assert.Equal(2, row.SeedRevision);
    }

    [Fact]
    public async Task An_edited_article_is_never_overwritten()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter("Alter Titel"), OneTerm, 1);

        var service = NewService(ctx);
        var chapter = (await service.GetAllChaptersAsync()).Single();
        var article = (await service.GetAllArticlesAsync(chapter.Id)).Single();

        await service.RefreshArticleAsync(article.Id,
            new HandbookArticleInput(chapter.Id, article.Slug, "Von Hand umformuliert", article.Summary,
                article.ContentHtml, article.RoleplayHtml, article.DiagramKey, article.NavKey, 0, true),
            Leader());

        // a later deploy ships a newer revision of the very same article
        await SeedAsync(ctx, OneChapter("Neuer Titel"), OneTerm, 2);

        await using var check = ctx.NewContext();
        var row = await check.HandbuchArtikel.SingleAsync(a => a.SeedKey == "art-a");
        Assert.Equal("Von Hand umformuliert", row.Title);
        Assert.True(row.IsCustomised);
    }

    [Fact]
    public async Task A_deleted_article_is_not_revived()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        var chapter = (await service.GetAllChaptersAsync()).Single();
        var article = (await service.GetAllArticlesAsync(chapter.Id)).Single();
        await service.DeleteArticleAsync(article.Id, Leader());

        await SeedAsync(ctx, OneChapter(), OneTerm, 2);

        await using var check = ctx.NewContext();
        var rows = await check.HandbuchArtikel.IgnoreQueryFilters()
            .Where(a => a.SeedKey == "art-a").ToListAsync();
        Assert.Single(rows);
        Assert.True(rows[0].IsDeleted);
    }

    [Fact]
    public async Task Steps_are_replaced_with_their_article_rather_than_accumulating()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(stepTitle: "Erster Schritt"), OneTerm, 1);

        await SeedAsync(ctx, OneChapter(stepTitle: "Anderer Schritt"), OneTerm, 2);

        await using var check = ctx.NewContext();
        var steps = await check.HandbuchSchritte.ToListAsync();
        Assert.Single(steps);
        Assert.Equal("Anderer Schritt", steps[0].Title);
    }

    // --- the service ------------------------------------------------------

    [Fact]
    public async Task An_article_in_a_hidden_chapter_is_hidden_too()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        Assert.NotNull(await service.GetArticleAsync("anmelden"));

        await using (var db = ctx.NewContext())
        {
            (await db.HandbuchKapitel.SingleAsync()).IsVisible = false;
            await db.SaveChangesAsync();
        }

        // the chapter is how a reader reaches an article; hiding it must hide the article behind it
        Assert.Null(await service.GetArticleAsync("anmelden"));
        Assert.Null(await service.GetArticleForNavKeyAsync("dashboard"));
    }

    [Fact]
    public async Task The_help_button_resolves_a_menu_key_to_its_article()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);

        Assert.Equal("anmelden", (await service.GetArticleForNavKeyAsync("dashboard"))?.Slug);
        Assert.Null(await service.GetArticleForNavKeyAsync("gibt-es-nicht"));
        Assert.Null(await service.GetArticleForNavKeyAsync(""));
    }

    [Fact]
    public async Task A_glossary_term_carries_its_article_and_its_synonyms()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var term = (await NewService(ctx).GetGlossaryAsync()).Single();

        Assert.Equal("Codename", term.Term);
        Assert.Equal("Deckname", term.Synonyms);
        Assert.Equal("anmelden", term.ArticleSlug);
    }

    [Fact]
    public async Task A_deleted_article_keeps_its_address_blocked()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        var chapter = (await service.GetAllChaptersAsync()).Single();
        var article = (await service.GetAllArticlesAsync(chapter.Id)).Single();
        await service.DeleteArticleAsync(article.Id, Leader());

        // the slug is a unique index, and a soft-deleted row still owns it - say so instead of failing on the index
        var clash = new HandbookArticleInput(chapter.Id, "anmelden", "Zweiter Versuch", null, null, null, null, null, 0, true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateArticleAsync(clash, Leader()));
        Assert.Contains("Papierkorb", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_chapter_with_articles_cannot_be_deleted()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        var chapter = (await service.GetAllChaptersAsync()).Single();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteChapterAsync(chapter.Id, Leader()));
    }

    // --- permissions ------------------------------------------------------

    [Fact]
    public async Task An_agent_may_read_but_not_write()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        Assert.Single(await service.GetChaptersAsync());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateChapterAsync(
                new HandbookChapterInput("neu", "Neu", null, null, 0, true), Agent()));
    }

    [Fact]
    public async Task An_hrb_without_a_leadership_rank_may_write()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        var created = await service.CreateChapterAsync(
            new HandbookChapterInput("einarbeitung", "Einarbeitung", null, null, 5, true), JuniorHrb());

        Assert.Equal("einarbeitung", created.Slug);
    }

    [Fact]
    public async Task The_read_only_supervision_is_refused_before_anything_is_written()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateChapterAsync(
                new HandbookChapterInput("neu", "Neu", null, null, 0, true), OnlyReader()));

        await using var check = ctx.NewContext();
        Assert.Equal(1, await check.HandbuchKapitel.CountAsync());
    }

    [Fact]
    public async Task Saving_a_shipped_article_marks_it_as_the_author_s()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var service = NewService(ctx);
        var chapter = (await service.GetAllChaptersAsync()).Single();
        var article = (await service.GetAllArticlesAsync(chapter.Id)).Single();

        await service.RefreshArticleAsync(article.Id,
            new HandbookArticleInput(chapter.Id, article.Slug, article.Title, article.Summary,
                article.ContentHtml, null, null, null, 0, true), Leader());

        await using var check = ctx.NewContext();
        Assert.True((await check.HandbuchArtikel.SingleAsync()).IsCustomised);
    }

    [Fact]
    public async Task A_slug_is_cleaned_rather_than_rejected()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, OneChapter(), OneTerm, 1);

        var created = await NewService(ctx).CreateChapterAsync(
            new HandbookChapterInput("  Akten   Führen!! ", "Akten führen", null, null, 1, true), Leader());

        // a slug is a URL segment: validated on write, with umlauts transliterated so links stay readable
        Assert.Equal("akten-fuehren", created.Slug);
    }

    // --- the shipped content ---------------------------------------------

    [Fact]
    public void Every_shipped_key_and_slug_is_unique()
    {
        var chapterKeys = HandbookContent.Chapters.Select(c => c.Key).ToList();
        var chapterSlugs = HandbookContent.Chapters.Select(c => c.Slug).ToList();
        var articles = HandbookContent.Chapters.SelectMany(c => c.Articles).ToList();
        var termKeys = HandbookContent.Terms.Select(t => t.Key).ToList();

        Assert.Equal(chapterKeys.Count, chapterKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(chapterSlugs.Count, chapterSlugs.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(articles.Count, articles.Select(a => a.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(articles.Count, articles.Select(a => a.Slug).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(termKeys.Count, termKeys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Shipped slugs are written raw; the editor cleans what it is given, so the two must already agree.</summary>
    [Fact]
    public void Every_shipped_slug_is_already_url_clean()
    {
        var slugs = HandbookContent.Chapters.Select(c => c.Slug)
            .Concat(HandbookContent.Chapters.SelectMany(c => c.Articles).Select(a => a.Slug))
            .ToList();

        var dirty = slugs
            .Where(s => s != s.ToLowerInvariant()
                || s.Any(ch => !((ch >= 'a' && ch <= 'z') || char.IsDigit(ch) || ch == '-'))
                || s.StartsWith('-') || s.EndsWith('-') || s.Contains("--", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(dirty);
    }

    /// <summary>A diagram key that resolves to nothing renders nothing - silently, which is why this is a test.</summary>
    [Fact]
    public void Every_shipped_diagram_key_names_a_drawing()
    {
        var known = NOOSE_Website.Components.Pages.Handbook.Shared.HandbookDiagram.Known
            .Select(d => d.Key)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = HandbookContent.Chapters
            .SelectMany(c => c.Articles)
            .Where(a => a.DiagramKey is not null && !known.Contains(a.DiagramKey))
            .Select(a => a.Key)
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>A misspelled menu key leaves the help button dark on exactly the page it was meant for.</summary>
    [Fact]
    public void Every_shipped_nav_key_names_a_menu_entry()
    {
        var known = NavCatalog.Internal.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);

        var unknown = HandbookContent.Chapters
            .SelectMany(c => c.Articles)
            .Where(a => a.NavKey is not null && !known.Contains(a.NavKey))
            .Select(a => a.Key)
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>A term that points at an article which does not exist would link nowhere.</summary>
    [Fact]
    public void Every_shipped_term_points_at_an_article_that_exists()
    {
        var articleKeys = HandbookContent.Chapters
            .SelectMany(c => c.Articles)
            .Select(a => a.Key)
            .ToHashSet(StringComparer.Ordinal);

        var dangling = HandbookContent.Terms
            .Where(t => t.ArticleKey is not null && !articleKeys.Contains(t.ArticleKey))
            .Select(t => t.Key)
            .ToList();

        Assert.Empty(dangling);
    }

    /// <summary>The real shipped book against the real schema - the closest a test gets to a first start.</summary>
    [Fact]
    public async Task The_shipped_handbook_seeds_and_renders()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, HandbookContent.Chapters, HandbookContent.Terms, HandbookContent.Revision);

        var service = NewService(ctx);
        var chapters = await service.GetChaptersAsync();
        var terms = await service.GetGlossaryAsync();

        Assert.Equal(HandbookContent.Chapters.Count, chapters.Count);
        Assert.Equal(
            HandbookContent.Chapters.Sum(c => c.Articles.Length),
            chapters.Sum(c => c.Articles.Count));
        Assert.Equal(HandbookContent.Terms.Count, terms.Count);

        // every chapter carries text: an empty one renders as a heading with nothing under it
        Assert.All(chapters, c => Assert.NotEmpty(c.Articles));
        var first = await service.GetArticleAsync(chapters[0].Articles[0].Slug);
        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!.ContentHtml));
    }

    /// <summary>The shipped markup has to survive the sanitizer; a stripped tag must show up here, not on a screen.</summary>
    [Fact]
    public async Task The_shipped_markup_survives_the_filter()
    {
        using var ctx = new SqliteTestContext();
        await SeedAsync(ctx, HandbookContent.Chapters, HandbookContent.Terms, HandbookContent.Revision);

        await using var check = ctx.NewContext();
        var written = await check.HandbuchArtikel
            .Where(a => a.ContentHtml != null)
            .Select(a => new { a.SeedKey, a.ContentHtml })
            .ToListAsync();

        Assert.NotEmpty(written);
        Assert.All(written, w => Assert.Contains("<p>", w.ContentHtml!, StringComparison.Ordinal));
    }

    /// <summary>The term itself is uniquely indexed, so a duplicate would break the very first start.</summary>
    [Fact]
    public void Every_shipped_term_is_written_only_once()
    {
        var duplicates = HandbookContent.Terms
            .GroupBy(t => t.Term, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>A menu entry without an article is a page whose help button stays dark. New page, new article.</summary>
    /// <remarks>
    /// The exceptions are entries that are a second door onto a page that already has one - an article carries a
    /// single menu key, so the two cannot both claim it.
    /// </remarks>
    [Fact]
    public void Every_menu_entry_has_an_article()
    {
        string[] secondDoors =
        [
            // the same page as "tickets", filtered to the tickets you are attached to
            "tickets.beteiligt",
        ];

        var covered = HandbookContent.Chapters
            .SelectMany(c => c.Articles)
            .Where(a => a.NavKey is not null)
            .Select(a => a.NavKey!)
            .ToHashSet(StringComparer.Ordinal);

        var missing = NavCatalog.Internal
            .Select(e => e.Key)
            .Where(k => !covered.Contains(k) && !secondDoors.Contains(k, StringComparer.Ordinal))
            .ToList();

        Assert.Empty(missing);
    }
}
