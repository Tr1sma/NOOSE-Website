using System.Security.Claims;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Kasse;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="KassenService"/> over in-memory SQLite.</summary>
public sealed class KassenServiceTests
{
    private static readonly DateTime T0 = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.JuniorAgent).Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("tl").WithRank(Rank.SpecialAgent).AsTeamLead().Build();

    private static KassenService Build(SqliteTestContext ctx)
    {
        var caseNo = Substitute.For<ICaseNumberService>();
        var seq = 0;
        caseNo.NextAsync(Arg.Any<AppDbContext>(), "KAS", Arg.Any<CancellationToken>())
            .Returns(_ => $"NOOSE-KAS-2026-{++seq:0000}");
        return new KassenService(ctx.Factory, caseNo);
    }

    private static KassenBuchungInput Input(KassenKonto account, KassenBuchungArt kind, decimal amount, DateTime ts, string? reason = null)
        => new() { Account = account, Kind = kind, Amount = amount, Timestamp = ts, Reason = reason };

    [Fact]
    public async Task Book_FoldsRunningBalance_ChronologicallyPerRow()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 1000, T0), Leader());
        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Auszahlung, 300, T0.AddMinutes(1)), Leader());
        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 500, T0.AddMinutes(2)), Leader());

        Assert.Equal(1200m, await svc.GetBalanceAsync(KassenKonto.Schwarzgeld));

        var ledger = await svc.GetLedgerAsync(KassenKonto.Schwarzgeld);
        // newest first
        Assert.Equal(new[] { 1200m, 700m, 1000m }, ledger.Select(r => r.BalanceAfter).ToArray());
        Assert.Equal(new[] { 500m, -300m, 1000m }, ledger.Select(r => r.Delta).ToArray());
    }

    [Fact]
    public async Task Correction_SetsAbsoluteBalance()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await svc.BookAsync(Input(KassenKonto.Gruengeld, KassenBuchungArt.Einzahlung, 1000, T0), Leader());
        await svc.BookAsync(Input(KassenKonto.Gruengeld, KassenBuchungArt.Korrektur, 46_000_028, T0.AddMinutes(1)), Leader());

        Assert.Equal(46_000_028m, await svc.GetBalanceAsync(KassenKonto.Gruengeld));
    }

    [Fact]
    public async Task Withdrawal_ExceedingBalance_Throws_ButBoundaryToZeroIsAllowed()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 100, T0), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Auszahlung, 200, T0.AddMinutes(1)), Leader()));

        // exactly to zero is fine
        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Auszahlung, 100, T0.AddMinutes(1)), Leader());
        Assert.Equal(0m, await svc.GetBalanceAsync(KassenKonto.Schwarzgeld));
    }

    [Fact]
    public async Task Book_RejectsNegativeAmount_AndZeroForFlows()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, -5, T0), Leader()));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 0, T0), Leader()));
    }

    [Fact]
    public async Task Accounts_AreIndependent()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 1000, T0), Leader());

        var summaries = await svc.GetSummariesAsync();
        Assert.Equal(1000m, summaries.Single(s => s.Account == KassenKonto.Schwarzgeld).Balance);
        Assert.Equal(0m, summaries.Single(s => s.Account == KassenKonto.Gruengeld).Balance);
    }

    [Fact]
    public async Task Book_AssignsCaseNumber()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        var created = await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 5, T0), Leader());
        Assert.Equal("NOOSE-KAS-2026-0001", created.CaseNumber);
    }

    [Fact]
    public async Task Delete_RemovesBookingFromBalance()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        var a = await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 1000, T0), Leader());
        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 500, T0.AddMinutes(1)), Leader());
        Assert.Equal(1500m, await svc.GetBalanceAsync(KassenKonto.Schwarzgeld));

        await svc.DeleteAsync(a.Id, Leader());
        Assert.Equal(500m, await svc.GetBalanceAsync(KassenKonto.Schwarzgeld));
    }

    [Fact]
    public async Task Trash_ReturnsOnlyDeleted_AndRestoreClearsFlag()
    {
        // the soft-delete interceptor is not wired in the test context, so seed the deleted row directly
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.KassenBuchungen.Add(new KassenBuchung
            {
                Id = "del", CaseNumber = "NOOSE-KAS-2026-9001", Account = KassenKonto.Schwarzgeld,
                Kind = KassenBuchungArt.Einzahlung, Amount = 100, Timestamp = T0, IsDeleted = true, DeletedAt = T0,
            });
            db.KassenBuchungen.Add(new KassenBuchung
            {
                Id = "live", CaseNumber = "NOOSE-KAS-2026-9002", Account = KassenKonto.Schwarzgeld,
                Kind = KassenBuchungArt.Einzahlung, Amount = 40, Timestamp = T0,
            });
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var trash = await svc.GetTrashAsync();
        Assert.Single(trash);
        Assert.Equal("del", trash[0].Id);
        // deleted row is excluded from the balance
        Assert.Equal(40m, await svc.GetBalanceAsync(KassenKonto.Schwarzgeld));

        await svc.RestoreAsync("del", Leader());
        Assert.Empty(await svc.GetTrashAsync());
        Assert.Equal(140m, await svc.GetBalanceAsync(KassenKonto.Schwarzgeld));
    }

    [Fact]
    public async Task Book_ResolvesBookedByCodename()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            var a = Seed.Agent("lead");
            a.Codename = "Falcon";
            db.Users.Add(a);
            db.SaveChanges();
        }
        var svc = Build(ctx);
        await svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 10, T0), Leader());

        var ledger = await svc.GetLedgerAsync(KassenKonto.Schwarzgeld);
        Assert.Equal("Falcon", ledger[0].BookedByCodename);
    }

    [Fact]
    public async Task NonLeadership_CannotBook()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 10, T0), Junior()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.BookAsync(Input(KassenKonto.Schwarzgeld, KassenBuchungArt.Einzahlung, 10, T0), OnlyReader()));
    }
}
