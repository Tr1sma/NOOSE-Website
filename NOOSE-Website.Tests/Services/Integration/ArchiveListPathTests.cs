using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>An archived record leaves the listings and the pickers, and nothing else.</summary>
public sealed class ArchiveListPathTests
{
    private static PersonService Build(SqliteTestContext ctx)
        => new(ctx.Factory,
            Substitute.For<IFileStorageService>(),
            Substitute.For<IProfileSuggestionService>(),
            Substitute.For<ICaseNumberService>(),
            Substitute.For<IThreatScoreService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<NOOSE_Website.Services.Public.IPublicWantedService>());

    private static ViewerScope Leader
        => new(MayClassifiedRead: true, MayAllTaskforces: true, MeId: "lead", PartnerAgency: null,
            IsLeadership: true, IsInternalAgent: true);

    private static async Task<SqliteTestContext> StockAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        db.Users.Add(Seed.Agent("lead", Rank.Director));
        db.People.Add(Seed.Person("aktiv", "Aktive Akte"));
        db.People.Add(Seed.Person("archiv", "Archivierte Akte", p => p.IsArchived = true));
        await db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task GetListAsync_shows_the_active_stock_by_default()
    {
        using var ctx = await StockAsync();
        var rows = await Build(ctx).GetListAsync(Leader);
        Assert.Equal(["aktiv"], rows.Select(p => p.Id));
    }

    [Fact]
    public async Task GetListAsync_can_include_the_archive()
    {
        using var ctx = await StockAsync();
        var rows = await Build(ctx).GetListAsync(Leader, ArchiveFilter.Including);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task GetListAsync_can_show_the_archive_alone()
    {
        using var ctx = await StockAsync();
        var rows = await Build(ctx).GetListAsync(Leader, ArchiveFilter.Only);
        Assert.Equal(["archiv"], rows.Select(p => p.Id));
    }

    [Fact]
    public async Task SearchAsync_never_offers_an_archived_record()
    {
        using var ctx = await StockAsync();
        var hits = await Build(ctx).SearchAsync("Akte", isLeadership: true);
        Assert.Equal(["aktiv"], hits.Select(p => p.Id));
    }

    [Fact]
    public async Task The_detail_page_still_opens_an_archived_record()
    {
        using var ctx = await StockAsync();
        var person = await Build(ctx).GetDetailAsync("archiv", Leader);
        Assert.NotNull(person);
        Assert.True(person!.IsArchived);
    }

    [Fact]
    public async Task A_link_to_an_archived_record_still_resolves()
    {
        using var ctx = await StockAsync();
        await using var db = ctx.NewContext();
        var map = await RecordsReference.ResolveAsync(db, [(nameof(Person), "archiv")]);
        // the display carries the case number too; what matters is that it resolves at all
        Assert.StartsWith("Archivierte Akte", map[(nameof(Person), "archiv")].Display);
    }

    [Fact]
    public async Task Restoring_from_the_trash_leaves_the_archive_flag_alone()
    {
        using var ctx = await StockAsync();
        var lead = ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

        // the soft-delete interceptor is not wired in the test host, so mark the row the way it would
        await using (var db = ctx.NewContext())
        {
            var row = await db.People.SingleAsync(p => p.Id == "archiv");
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var service = Build(ctx);
        Assert.Contains(await service.GetTrashAsync(), p => p.Id == "archiv");

        await service.RestoreAsync("archiv", lead);
        await using (var db = ctx.NewContext())
        {
            var person = await db.People.SingleAsync(p => p.Id == "archiv");
            Assert.False(person.IsDeleted);
            Assert.True(person.IsArchived);
        }
    }
}
