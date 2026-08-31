using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The public law page: only what somebody released, and only while the module is on.</summary>
public sealed class PublicLawServiceTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal Senior()
        => ClaimsPrincipalBuilder.Agent("senior").WithRank(Rank.SeniorSpecialAgent).WithCodename("Kite").Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static ClaimsPrincipal Citizen()
        => ClaimsPrincipalBuilder.Agent("buerger").WithStatus(AgentStatus.Civilian).Build();

    private sealed record Host(PublicLawService Service, LawService Laws, IMemoryCache Cache);

    private static Host NewHost(SqliteTestContext ctx)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var modules = new PublicModuleService(ctx.Factory, cache);
        var service = new PublicLawService(ctx.Factory, modules, cache);
        return new Host(service, new LawService(ctx.Factory, service), cache);
    }

    private static async Task<SqliteTestContext> SeededAsync(bool lawOn = true)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Law)).IsEnabled = lawOn;
        db.Laws.AddRange(
            new Law { Id = "s1", LawBook = "StGB", Paragraph = "§ 1", Title = "Landfriedensbruch", Text = "Wer …", Sentence = "bis 5 Jahre" },
            new Law { Id = "s2", LawBook = "StGB", Paragraph = "§ 2", Title = "Waffenbesitz", Text = "Wer …" },
            new Law { Id = "v1", LawBook = "StVO", Paragraph = "§ 1", Title = "Vorfahrt", Text = "Wer …" });
        await db.SaveChangesAsync();
        return ctx;
    }

    private static async Task ModuleAsync(SqliteTestContext ctx, Host host, bool on)
    {
        await using var db = ctx.NewContext();
        (await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Law)).IsEnabled = on;
        await db.SaveChangesAsync();
        host.Cache.Remove("OeffentlicheModule");
    }

    // ---- what goes out ----

    [Fact]
    public async Task NothingIsPublicUntilSomebodySaysSo()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        Assert.Empty((await host.Service.GetPublishedAsync()).Books);
    }

    [Fact]
    public async Task AReleasedParagraph_AppearsUnderItsLawBook()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await host.Service.SetPublicAsync("s1", true, Leader());

        var book = Assert.Single((await host.Service.GetPublishedAsync()).Books);
        Assert.Equal("StGB", book.Name);
        var entry = Assert.Single(book.Entries);
        Assert.Equal("§ 1", entry.Paragraph);
        Assert.Equal("Landfriedensbruch", entry.Title);
        Assert.Equal("bis 5 Jahre", entry.Sentence);
    }

    [Fact]
    public async Task ParagraphsAreGroupedByBookInReadingOrder()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        foreach (var id in new[] { "v1", "s2", "s1" })
        {
            await host.Service.SetPublicAsync(id, true, Leader());
        }

        var books = (await host.Service.GetPublishedAsync()).Books;
        Assert.Equal(["StGB", "StVO"], books.Select(b => b.Name));
        Assert.Equal(["§ 1", "§ 2"], books[0].Entries.Select(e => e.Paragraph));
    }

    [Fact]
    public async Task WithdrawingAReleaseTakesTheParagraphOff()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SetPublicAsync("s1", true, Leader());

        await host.Service.SetPublicAsync("s1", false, Leader());

        Assert.Empty((await host.Service.GetPublishedAsync()).Books);
    }

    [Fact]
    public async Task ADeletedParagraph_LeavesThePublicPageAtOnce()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SetPublicAsync("s1", true, Leader());
        Assert.Single((await host.Service.GetPublishedAsync()).Books);

        // deletion goes through the internal service, which drops the public snapshot on its way out
        await host.Laws.DeleteAsync("s1", Leader());

        Assert.Empty((await host.Service.GetPublishedAsync()).Books);
    }

    [Fact]
    public async Task ACorrectedParagraph_ShowsItsNewTextAtOnce()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SetPublicAsync("s1", true, Leader());
        Assert.Single((await host.Service.GetPublishedAsync()).Books);

        await host.Laws.RefreshAsync("s1", new LawInput
        {
            LawBook = "StGB", Paragraph = "§ 1", Title = "Landfriedensbruch", Text = "Neuer Wortlaut.",
        }, Leader());

        var entry = (await host.Service.GetPublishedAsync()).Books.Single().Entries.Single();
        Assert.Equal("Neuer Wortlaut.", entry.Text);
    }

    // ---- module ----

    [Fact]
    public async Task TheModuleBeingOff_HidesEveryParagraph()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SetPublicAsync("s1", true, Leader());

        await ModuleAsync(ctx, host, false);

        Assert.Empty((await host.Service.GetPublishedAsync()).Books);
    }

    [Fact]
    public async Task ReleasingWithTheModuleOff_IsRefused()
    {
        using var ctx = await SeededAsync(lawOn: false);
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.SetPublicAsync("s1", true, Leader()));
    }

    [Fact]
    public async Task WithdrawingWithTheModuleOff_Works()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SetPublicAsync("s1", true, Leader());
        await ModuleAsync(ctx, host, false);

        await host.Service.SetPublicAsync("s1", false, Leader());

        await ModuleAsync(ctx, host, true);
        Assert.Empty((await host.Service.GetPublishedAsync()).Books);
    }

    // ---- guards and the panel ----

    [Fact]
    public async Task ThePanelListsEveryParagraphWithItsReleaseFlag()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.SetPublicAsync("s2", true, Leader());

        var rows = await host.Service.GetAllAsync(Leader());
        Assert.Equal(3, rows.Count);
        Assert.True(rows.Single(r => r.Id == "s2").IsPublic);
        Assert.False(rows.Single(r => r.Id == "s1").IsPublic);
    }

    [Fact]
    public async Task TheReadOnlySupervision_ReadsButDoesNotRelease()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        Assert.Equal(3, (await host.Service.GetAllAsync(OnlyReader())).Count);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.SetPublicAsync("s1", true, OnlyReader()));
    }

    [Fact]
    public async Task ARankThreeAgent_MayNotRelease()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.SetPublicAsync("s1", true, Senior()));
    }

    [Fact]
    public async Task ARankThreeAgent_DoesNotEvenReadThePanel()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAllAsync(Senior()));
    }

    [Fact]
    public async Task ASignedInCitizen_IsOutOnBothSides()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAllAsync(Citizen()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.SetPublicAsync("s1", true, Citizen()));
    }
}
