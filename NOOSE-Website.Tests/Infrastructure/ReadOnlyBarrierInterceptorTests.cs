using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure.Authorization;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>What the write barrier lets a partner through, and what it still refuses.</summary>
/// <remarks>
/// The barrier is the last word on a partner write: the service guards are looser for the citizen area, so a hole
/// here would open the whole record surface. The audit interceptor is deliberately NOT attached — this fixture asks
/// what the barrier does, so the rows carry their author by hand.
/// </remarks>
public sealed class ReadOnlyBarrierInterceptorTests
{
    private const string PartnerId = "partner";
    private const string ProfileId = "profil1";

    private sealed class FixedUser(CurrentUserInfo info) : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(info);

        public CurrentUserInfo Get() => info;
    }

    private static CurrentUserInfo Partner() => new(PartnerId, "Ward", false, true, false);

    private static CurrentUserInfo OnlyReader() => new("aufsicht", "Owl", true, false, false);

    /// <summary>A context on the shared database with the barrier attached, acting as the given user.</summary>
    private static AppDbContext Guarded(SqliteTestContext ctx, CurrentUserInfo user)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new ReadOnlyBarrierInterceptor(new FixedUser(user)))
            .Options);

    private static Ticket Ticket(string id, string author) => new()
    {
        Id = id,
        CaseNumber = $"NOOSE-T-2026-{id}",
        CitizenProfileId = ProfileId,
        Status = TicketStatus.Offen,
        Subject = "Anfrage an die Führungsebene",
        LastActivityAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
        CreatedById = author,
    };

    [Fact]
    public async Task A_partner_may_write_their_own_civilian_identity()
    {
        using var ctx = new SqliteTestContext();
        await using var db = Guarded(ctx, Partner());

        db.BuergerProfile.Add(new BuergerProfil { Id = ProfileId, UserId = PartnerId, FirstName = "Trevor", LastName = "Ward" });

        await db.SaveChangesAsync();
        await using var check = ctx.NewContext();
        Assert.Equal(1, await check.BuergerProfile.CountAsync());
    }

    [Fact]
    public async Task A_partner_may_open_a_ticket_with_its_first_message()
    {
        using var ctx = new SqliteTestContext();
        await using var db = Guarded(ctx, Partner());

        db.Tickets.Add(Ticket("0001", PartnerId));
        db.TicketNachrichten.Add(new TicketNachricht
        {
            TicketId = "0001",
            Audience = TicketMessageAudience.Buerger,
            Text = "Wir bitten um eine Einschätzung zu einem laufenden Verfahren.",
            AuthorIsCitizen = true,
        });

        await db.SaveChangesAsync();
        await using var check = ctx.NewContext();
        Assert.Equal(1, await check.Tickets.CountAsync());
    }

    [Fact]
    public async Task A_partner_may_answer_in_a_ticket_they_opened_themselves()
    {
        // the reply moves the status and the activity stamp of the ticket, so the create carve-out alone is not enough
        using var ctx = new SqliteTestContext();
        await using (var seed = ctx.NewContext())
        {
            seed.Tickets.Add(Ticket("0001", PartnerId));
            await seed.SaveChangesAsync();
        }

        await using var db = Guarded(ctx, Partner());
        var row = await db.Tickets.SingleAsync();
        row.Status = TicketStatus.InBearbeitung;
        row.LastActivityAt = DateTime.UtcNow;
        db.TicketNachrichten.Add(new TicketNachricht
        {
            TicketId = row.Id,
            Audience = TicketMessageAudience.Buerger,
            Text = "Wir hängen den Vorgang an unsere Akte an.",
            AuthorIsCitizen = true,
        });

        await db.SaveChangesAsync();
        await using var check = ctx.NewContext();
        Assert.Equal(TicketStatus.InBearbeitung, (await check.Tickets.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_partner_may_file_a_tip_with_its_first_message()
    {
        using var ctx = new SqliteTestContext();
        await using var db = Guarded(ctx, Partner());

        db.Hinweise.Add(new Hinweis
        {
            Id = "h1",
            CaseNumber = "NOOSE-H-2026-0001",
            CitizenProfileId = ProfileId,
            Status = TipStatus.Neu,
            Text = "Der gesuchte Van stand heute Nacht am Hafen.",
        });
        db.HinweisNachrichten.Add(new HinweisNachricht
        {
            HinweisId = "h1",
            Audience = TipMessageAudience.Buerger,
            Text = "Das Kennzeichen kam aus Blaine County.",
            AuthorIsCitizen = true,
        });

        await db.SaveChangesAsync();
        await using var check = ctx.NewContext();
        Assert.Equal(1, await check.Hinweise.CountAsync());
    }

    [Fact]
    public async Task A_partner_may_not_touch_a_ticket_somebody_else_opened()
    {
        using var ctx = new SqliteTestContext();
        await using (var seed = ctx.NewContext())
        {
            seed.Tickets.Add(Ticket("0001", "buerger1"));
            await seed.SaveChangesAsync();
        }

        await using var db = Guarded(ctx, Partner());
        var row = await db.Tickets.SingleAsync();
        row.Status = TicketStatus.Geschlossen;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_partner_may_not_delete_their_own_ticket()
    {
        using var ctx = new SqliteTestContext();
        await using (var seed = ctx.NewContext())
        {
            seed.Tickets.Add(Ticket("0001", PartnerId));
            await seed.SaveChangesAsync();
        }

        await using var db = Guarded(ctx, Partner());
        db.Tickets.Remove(await db.Tickets.SingleAsync());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_read_only_supervision_files_its_own_civilian_identity_and_ticket()
    {
        // the person behind the supervision account plays a civilian too; none of this is record material
        using var ctx = new SqliteTestContext();
        await using var db = Guarded(ctx, OnlyReader());

        db.BuergerProfile.Add(new BuergerProfil
        {
            Id = ProfileId, UserId = "aufsicht", FirstName = "Owl", LastName = "Ward",
        });
        db.Tickets.Add(Ticket("0001", "aufsicht"));

        await db.SaveChangesAsync();
        await using var check = ctx.NewContext();
        Assert.Equal(1, await check.Tickets.CountAsync());
    }

    [Fact]
    public async Task The_read_only_supervision_still_writes_no_agency_content()
    {
        // the second axis: what a partner may author is record material, and the supervision authors none of it
        using var ctx = new SqliteTestContext();
        await using var db = Guarded(ctx, OnlyReader());

        db.Documents.Add(new Document { Id = "d1", Title = "Vermerk", CreatedById = "aufsicht" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }
}
