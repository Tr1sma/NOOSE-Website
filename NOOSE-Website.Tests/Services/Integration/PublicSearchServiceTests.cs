using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The public search: only published rows, only through the services that own them.</summary>
public sealed class PublicSearchServiceTests
{
    private const string PersonId = "p1";

    private static readonly string[] AllModules =
    [
        PublicModules.PublicSearch, PublicModules.Wanted, PublicModules.WantedArchive,
        PublicModules.WantedVehicles, PublicModules.Press,
    ];

    private sealed record Host(PublicSearchService Service, IMemoryCache Cache, IPressReleaseService Press);

    private static Host NewHost(SqliteTestContext ctx, PublicPressSnapshot? press = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ctx.Connection).Options;
        var factory = new TestDbContextFactory(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var modules = new PublicModuleService(factory, cache);

        // the real notice service, because the belt is the thing that must not be bypassed; the rest are doubles,
        // since their own read paths are covered where they live
        var wanted = new PublicWantedService(factory, modules, Substitute.For<ICaseNumberService>(),
            Substitute.For<IFileStorageService>(), Substitute.For<IPublicWantedPhotoStorageService>(),
            Substitute.For<INotificationService>(), new TipPriorityService(factory),
            Substitute.For<IDiscordWebhookService>(), Substitute.For<IPressReleaseService>(), cache);

        var pressService = Substitute.For<IPressReleaseService>();
        pressService.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(press ?? PublicPressSnapshot.Empty));

        var factions = Substitute.For<IPublicFactionProfileService>();
        factions.GetBoardAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(PublicFactionBoard.Empty));
        var warnings = Substitute.For<IPublicWarningService>();
        warnings.GetPublishedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(PublicWarningSnapshot.Empty));
        var reports = Substitute.For<IPublicReportService>();
        reports.GetPublishedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(PublicReportSnapshot.Empty));
        var pages = Substitute.For<IPublicPageService>();
        pages.GetPublishedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(PublicPageSnapshot.Empty));
        var laws = Substitute.For<IPublicLawService>();
        laws.GetPublishedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(PublicLawSnapshot.Empty));

        var service = new PublicSearchService(modules, wanted, factions, pressService, warnings, reports, pages, laws);
        return new Host(service, cache, pressService);
    }

    private static async Task<SqliteTestContext> SeededAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        foreach (var key in AllModules)
        {
            (await db.OeffentlicheModule.SingleAsync(m => m.Key == key)).IsEnabled = true;
        }
        db.People.Add(Seed.Person(PersonId, "Max Mustermann", p => p.CaseNumber = "NOOSE-P-2026-0001"));
        await db.SaveChangesAsync();
        return ctx;
    }

    private static OeffentlicheFahndung Notice(string caseNumber, PublicWantedStatus status, string name,
        Action<OeffentlicheFahndung>? tweak = null)
    {
        var row = new OeffentlicheFahndung
        {
            Id = Guid.NewGuid().ToString(),
            CaseNumber = caseNumber,
            Status = status,
            PersonId = PersonId,
            DisplayName = name,
            PublishedAt = DateTime.UtcNow.AddDays(-1),
        };
        tweak?.Invoke(row);
        return row;
    }

    private static async Task AddAsync(SqliteTestContext ctx, params object[] rows)
    {
        await using var db = ctx.NewContext();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task ModuleAsync(SqliteTestContext ctx, Host host, string key, bool on)
    {
        await using var db = ctx.NewContext();
        (await db.OeffentlicheModule.SingleAsync(m => m.Key == key)).IsEnabled = on;
        await db.SaveChangesAsync();
        host.Cache.Remove("OeffentlicheModule");
        host.Cache.Remove("OeffentlicheFahndungen");
    }

    // ---- the gate ----

    [Fact]
    public async Task ModuleOff_FindsNothing()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));
        await ModuleAsync(ctx, host, PublicModules.PublicSearch, false);

        var results = await host.Service.SearchAsync("Kupferdraht");

        Assert.Empty(results.Groups);
    }

    [Fact]
    public async Task AQueryBelowTheMinimum_IsRefusedRatherThanAnswered()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));

        var results = await host.Service.SearchAsync("Ku");

        Assert.Empty(results.Groups);
        Assert.Empty(results.Query);
    }

    [Fact]
    public async Task AQueryOfWeightlessCharactersDoesNotReturnTheWholeCorpus()
    {
        // a culture-sensitive comparison treats a string of only zero-width characters as equal at position zero,
        // so without the strip in Normalise these three would have matched every published row
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));

        var results = await host.Service.SearchAsync("​​​");

        Assert.Empty(results.Groups);
        Assert.Empty(results.Query);
    }

    [Fact]
    public async Task AnOverlongQueryIsCutRatherThanRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        var results = await host.Service.SearchAsync(new string('x', PublicSearchRules.MaxQueryLength + 50));

        Assert.Equal(PublicSearchRules.MaxQueryLength, results.Query.Length);
    }

    // ---- only what is published ----

    [Fact]
    public async Task OnlyPublishedNoticesAreFound()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx,
            Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht offen"),
            Notice("FA-2", PublicWantedStatus.Entwurf, "Kupferdraht Entwurf"),
            Notice("FA-3", PublicWantedStatus.Zurueckgezogen, "Kupferdraht zurückgezogen"),
            Notice("FA-4", PublicWantedStatus.Gefasst, "Kupferdraht gefasst"),
            Notice("FA-5", PublicWantedStatus.Veroeffentlicht, "Kupferdraht abgelaufen",
                f => f.ExpiresAt = DateTime.UtcNow.AddDays(-1)));

        var results = await host.Service.SearchAsync("Kupferdraht");
        var titles = results.Groups.SelectMany(g => g.Hits).Select(h => h.Title).ToList();

        Assert.Equal(["Kupferdraht offen"], titles);
    }

    [Fact]
    public async Task ANoticeWhoseFileWasSuppressedIsNotFound()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));
        Assert.Single((await host.Service.SearchAsync("Kupferdraht")).Groups);

        await using (var db = ctx.NewContext())
        {
            (await db.People.SingleAsync(p => p.Id == PersonId)).IsDeleted = true;
            await db.SaveChangesAsync();
        }
        host.Cache.Remove("OeffentlicheFahndungen");

        Assert.Empty((await host.Service.SearchAsync("Kupferdraht")).Groups);
    }

    [Fact]
    public async Task TheWantedModuleOff_TakesTheNoticesOutOfTheResult()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));
        await ModuleAsync(ctx, host, PublicModules.Wanted, false);

        Assert.Empty((await host.Service.SearchAsync("Kupferdraht")).Groups);
    }

    // ---- shape of a hit ----

    [Fact]
    public async Task ANoticeHitCarriesThePublicCaseNumberAndItsOwnAddress()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-2026-0007", PublicWantedStatus.Veroeffentlicht, "Kupferdraht",
            f => f.ChargeHtml = "<p>Diebstahl von <b>Kupferdraht</b> am Hafen</p>"));

        var hit = (await host.Service.SearchAsync("Kupferdraht")).Groups.Single().Hits.Single();

        Assert.Equal(PublicSearchArea.Fahndung, hit.Area);
        Assert.Equal("FA-2026-0007", hit.Reference);
        Assert.Equal("/gesucht/FA-2026-0007", hit.Href);
        // the charge matched and reaches the snippet as plain text, never as markup
        Assert.Contains("Kupferdraht", hit.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", hit.Snippet, StringComparison.Ordinal);
        // never the internal file number
        Assert.DoesNotContain("NOOSE-P-", hit.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("NOOSE-P-", hit.Reference!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APressReleaseIsFoundThroughItsBody()
    {
        using var ctx = await SeededAsync();
        var snapshot = new PublicPressSnapshot(
            [new PublicPressCard("PM-2026-0001", "Festnahme am Hafen", "Kurzfassung", DateTime.UtcNow)],
            new Dictionary<string, PublicPressView>(StringComparer.OrdinalIgnoreCase)
            {
                ["PM-2026-0001"] = new("PM-2026-0001", "Festnahme am Hafen", "Kurzfassung",
                    "<p>Sichergestellt wurde <b>Kupferdraht</b>.</p>", DateTime.UtcNow),
            });
        var host = NewHost(ctx, snapshot);

        var hit = (await host.Service.SearchAsync("Kupferdraht")).Groups.Single().Hits.Single();

        Assert.Equal(PublicSearchArea.Presse, hit.Area);
        Assert.Equal("/presse/PM-2026-0001", hit.Href);
        Assert.DoesNotContain("<b>", hit.Snippet, StringComparison.Ordinal);
    }

    // ---- capping and failure ----

    [Fact]
    public async Task AFullAreaSaysSoRatherThanCuttingSilently()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var rows = Enumerable.Range(1, PublicSearchRules.PerAreaLimit + 3)
            .Select(i => (object)Notice($"FA-{i}", PublicWantedStatus.Veroeffentlicht, $"Kupferdraht {i}"))
            .ToArray();
        await AddAsync(ctx, rows);

        var group = (await host.Service.SearchAsync("Kupferdraht")).Groups.Single();

        Assert.Equal(PublicSearchRules.PerAreaLimit, group.Hits.Count);
        Assert.True(group.Capped);
    }

    [Fact]
    public async Task ADarkSurfaceDoesNotBlankTheOthers()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));
        host.Press.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns<Task<PublicPressSnapshot>>(_ => throw new InvalidOperationException("Presse ist dunkel"));

        var results = await host.Service.SearchAsync("Kupferdraht");

        Assert.Single(results.Groups);
        Assert.Equal(PublicSearchArea.Fahndung, results.Groups[0].Area);
    }

    [Fact]
    public async Task GroupsKeepTheDeclaredOrder()
    {
        using var ctx = await SeededAsync();
        var snapshot = new PublicPressSnapshot(
            [new PublicPressCard("PM-1", "Kupferdraht Mitteilung", "Kurz", DateTime.UtcNow)],
            new Dictionary<string, PublicPressView>(StringComparer.OrdinalIgnoreCase));
        var host = NewHost(ctx, snapshot);
        await AddAsync(ctx, Notice("FA-1", PublicWantedStatus.Veroeffentlicht, "Kupferdraht"));

        var areas = (await host.Service.SearchAsync("Kupferdraht")).Groups.Select(g => g.Area).ToList();

        Assert.Equal([PublicSearchArea.Fahndung, PublicSearchArea.Presse], areas);
    }
}
