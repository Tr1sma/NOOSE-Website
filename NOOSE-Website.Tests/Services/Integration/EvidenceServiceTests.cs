using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Evidence;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Evidence;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="EvidenceService"/> over in-memory SQLite.</summary>
public sealed class EvidenceServiceTests
{
    private static readonly DateTime T0 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.JuniorAgent).Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("tl").WithRank(Rank.SpecialAgent).AsTeamLead().Build();

    private static EvidenceService Build(SqliteTestContext ctx)
    {
        var caseNo = Substitute.For<ICaseNumberService>();
        var seq = 0;
        caseNo.NextAsync(Arg.Any<AppDbContext>(), "ASS", Arg.Any<CancellationToken>())
            .Returns(_ => $"NOOSE-ASS-2026-{++seq:0000}");
        return new EvidenceService(ctx.Factory, caseNo, Substitute.For<IEvidenceImageStorageService>());
    }

    private static EvidenceEntryInput Entry(EvidenceEntryType type, Action<EvidenceEntryInput>? cfg = null)
    {
        var i = new EvidenceEntryInput
        {
            Type = type,
            OwnerType = EvidenceService.NooseOwner,
            Timestamp = T0,
            HandlerAgentId = "lead",
        };
        cfg?.Invoke(i);
        return i;
    }

    [Fact]
    public async Task CreateEntry_AssignsCaseNumber_AutoCreatesItem_AndPersistsLines()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        var created = await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.Lines.Add(new EvidenceLineInput { ItemName = "Twisted Burger", Quantity = 367 });
            i.Lines.Add(new EvidenceLineInput { ItemName = "Ripper Roo", Quantity = 367 });
        }), Leader());

        Assert.Equal("NOOSE-ASS-2026-0001", created.CaseNumber);
        using var db = ctx.NewContext();
        Assert.Equal(2, db.EvidenceItems.Count());
        Assert.Equal(2, db.EvidenceEntryLines.Count());
    }

    [Fact]
    public async Task CreateEntry_ReusesExistingItem_CaseInsensitive()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 5 })), Leader());
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "zyanid", Quantity = 2 })), Leader());

        using var db = ctx.NewContext();
        Assert.Equal(1, db.EvidenceItems.Count());
    }

    [Fact]
    public async Task OnHand_IsDepositsMinusWithdrawals()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 10 })), Leader());
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Withdrawal,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 3 })), Leader());

        var items = await svc.GetItemsAsync();
        Assert.Equal(7, items.Single(x => x.Item.Name == "Zyanid").OnHand);
    }

    [Fact]
    public async Task OnHand_ExcludesDeletedEntries()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 10 })), Leader());
        var itemId = (await svc.GetItemsAsync()).Single().Item.Id;

        // a soft-deleted deposit must not count toward on-hand (global query filter)
        using (var db = ctx.NewContext())
        {
            db.EvidenceEntries.Add(new EvidenceEntry
            {
                Id = "ghost", CaseNumber = "NOOSE-ASS-2026-9001", Type = EvidenceEntryType.Deposit,
                OwnerType = EvidenceService.NooseOwner, HandlerAgentId = "lead", Timestamp = T0,
                IsDeleted = true, DeletedAt = T0,
            });
            db.EvidenceEntryLines.Add(new EvidenceEntryLine { EntryId = "ghost", ItemId = itemId, Quantity = 100 });
            db.SaveChanges();
        }

        Assert.Equal(10, await svc.GetOnHandAsync(itemId));
    }

    [Fact]
    public async Task GetEntryDisplay_ResolvesNooseOwner()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        var e = await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Food", Quantity = 1 })), Leader());

        var display = await svc.GetEntryDisplayAsync(e.Id);
        Assert.Equal("NOOSE", display!.OwnerDisplay);
    }

    [Fact]
    public async Task GetEntryDisplay_ResolvesAgentOwner_ByCodename()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            var a = Seed.Agent("a1");
            a.Codename = "Falcon";
            db.Users.Add(a);
            db.SaveChanges();
        }
        var svc = Build(ctx);
        var e = await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.OwnerType = nameof(Agent);
            i.OwnerId = "a1";
            i.Lines.Add(new EvidenceLineInput { ItemName = "Food", Quantity = 1 });
        }), Leader());

        var display = await svc.GetEntryDisplayAsync(e.Id);
        Assert.Equal("Falcon", display!.OwnerDisplay);
    }

    [Fact]
    public async Task GetEntryDisplay_ResolvesPersonOwner()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            var p = Seed.Person("p1");
            p.Name = "John Doe";
            db.People.Add(p);
            db.SaveChanges();
        }
        var svc = Build(ctx);
        var e = await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.OwnerType = nameof(Person);
            i.OwnerId = "p1";
            i.Lines.Add(new EvidenceLineInput { ItemName = "Waffe", Quantity = 1 });
        }), Leader());

        var display = await svc.GetEntryDisplayAsync(e.Id);
        Assert.Contains("John Doe", display!.OwnerDisplay);
    }

    [Fact]
    public async Task UpdateEntry_ReplacesLines()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        var e = await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.Lines.Add(new EvidenceLineInput { ItemName = "A", Quantity = 1 });
            i.Lines.Add(new EvidenceLineInput { ItemName = "B", Quantity = 2 });
        }), Leader());

        await svc.UpdateEntryAsync(e.Id, Entry(EvidenceEntryType.Deposit, i =>
        {
            i.Lines.Add(new EvidenceLineInput { ItemName = "A", Quantity = 5 });
        }), Leader());

        using var db = ctx.NewContext();
        var lines = db.EvidenceEntryLines.Where(l => l.EntryId == e.Id).ToList();
        Assert.Single(lines);
        Assert.Equal(5, lines[0].Quantity);
    }

    [Fact]
    public async Task EntryTrash_ReturnsOnlyDeleted_AndRestoreClearsFlag()
    {
        // the audit/soft-delete interceptor is not wired in the test context, so seed the deleted row directly
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.EvidenceEntries.Add(new EvidenceEntry
            {
                Id = "del", CaseNumber = "NOOSE-ASS-2026-9001", Type = EvidenceEntryType.Deposit,
                OwnerType = EvidenceService.NooseOwner, HandlerAgentId = "lead", Timestamp = T0,
                IsDeleted = true, DeletedAt = T0,
            });
            db.EvidenceEntries.Add(new EvidenceEntry
            {
                Id = "live", CaseNumber = "NOOSE-ASS-2026-9002", Type = EvidenceEntryType.Deposit,
                OwnerType = EvidenceService.NooseOwner, HandlerAgentId = "lead", Timestamp = T0,
            });
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var trash = await svc.GetEntryTrashAsync();
        Assert.Single(trash);
        Assert.Equal("del", trash[0].Id);
        Assert.Single(await svc.GetEntriesAsync());

        await svc.RestoreEntryAsync("del", Leader());
        Assert.Empty(await svc.GetEntryTrashAsync());
        Assert.Equal(2, (await svc.GetEntriesAsync()).Count);
    }

    [Fact]
    public async Task Withdrawal_ExceedingStock_Throws()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 5 })), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateEntryAsync(Entry(EvidenceEntryType.Withdrawal,
                i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 6 })), Leader()));
    }

    [Fact]
    public async Task Withdrawal_WithinStock_Succeeds()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 10 })), Leader());
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Withdrawal,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 4 })), Leader());

        var itemId = (await svc.GetItemsAsync()).Single().Item.Id;
        Assert.Equal(6, await svc.GetOnHandAsync(itemId));
    }

    [Fact]
    public async Task Withdrawal_OfUnstockedItem_Throws()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateEntryAsync(Entry(EvidenceEntryType.Withdrawal,
                i => i.Lines.Add(new EvidenceLineInput { ItemName = "Nichts", Quantity = 1 })), Leader()));
    }

    [Fact]
    public async Task Withdrawal_AggregatesLinesPerItem()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 5 })), Leader());

        // two lines of the same item summing to 6 > 5 must be rejected
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateEntryAsync(Entry(EvidenceEntryType.Withdrawal, i =>
            {
                i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 3 });
                i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 3 });
            }), Leader()));
    }

    [Fact]
    public async Task UpdateWithdrawal_ExcludesOwnEntry_FromAvailability()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 10 })), Leader());
        var wd = await svc.CreateEntryAsync(Entry(EvidenceEntryType.Withdrawal,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 6 })), Leader());

        // editing this withdrawal up to 9 is allowed: available excluding itself is 10
        await svc.UpdateEntryAsync(wd.Id, Entry(EvidenceEntryType.Withdrawal,
            i => i.Lines.Add(new EvidenceLineInput { ItemName = "Zyanid", Quantity = 9 })), Leader());

        var itemId = (await svc.GetItemsAsync()).Single().Item.Id;
        Assert.Equal(1, await svc.GetOnHandAsync(itemId));
    }

    [Fact]
    public async Task GetEntriesForOwner_ReturnsOnlyThatOwner()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.OwnerType = nameof(Person); i.OwnerId = "p1";
            i.Lines.Add(new EvidenceLineInput { ItemName = "Waffe", Quantity = 1 });
        }), Leader());
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.OwnerType = nameof(Person); i.OwnerId = "p2";
            i.Lines.Add(new EvidenceLineInput { ItemName = "Geld", Quantity = 1 });
        }), Leader());

        var rows = await svc.GetEntriesForOwnerAsync(nameof(Person), "p1");
        Assert.Single(rows);
        Assert.Equal("p1", rows[0].Entry.OwnerId);
    }

    [Fact]
    public async Task GetEntriesForFactionMembers_IncludesMemberOwnedEntriesOnly()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Factions.Add(Seed.Faction("fac"));
            db.People.Add(Seed.Person("p1"));
            db.People.Add(Seed.Person("p2"));
            db.FactionMembers.Add(new FactionMember { Id = "m1", FactionId = "fac", PersonId = "p1" });
            db.SaveChanges();
        }
        var svc = Build(ctx);
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.OwnerType = nameof(Person); i.OwnerId = "p1";
            i.Lines.Add(new EvidenceLineInput { ItemName = "Waffe", Quantity = 1 });
        }), Leader());
        await svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit, i =>
        {
            i.OwnerType = nameof(Person); i.OwnerId = "p2";
            i.Lines.Add(new EvidenceLineInput { ItemName = "Geld", Quantity = 1 });
        }), Leader());

        var rows = await svc.GetEntriesForFactionMembersAsync("fac");
        Assert.Single(rows);
        Assert.Equal("p1", rows[0].Entry.OwnerId);
    }

    [Fact]
    public async Task CreateItem_RejectsDuplicateName()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);
        await svc.CreateItemAsync(new EvidenceItemInput { Name = "Zyanid" }, null, null, Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateItemAsync(new EvidenceItemInput { Name = "zyanid" }, null, null, Leader()));
    }

    [Fact]
    public async Task NonLeadership_CannotWrite()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateItemAsync(new EvidenceItemInput { Name = "X" }, null, null, Junior()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateEntryAsync(Entry(EvidenceEntryType.Deposit,
                i => i.Lines.Add(new EvidenceLineInput { ItemName = "X", Quantity = 1 })), OnlyReader()));
    }
}
