using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="PublicTemplateService"/>: who may edit, and what a template may contain.</summary>
public sealed class PublicTemplateServiceTests
{
    private const string Body = "Sehr geehrte/r BUERGER, Ihr Anliegen AKTENZEICHEN ist eingegangen.";

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.SpecialAgent).Build();

    private static ClaimsPrincipal Supervision()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static ClaimsPrincipal Partner()
        => ClaimsPrincipalBuilder.Agent("partner").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

    private static ClaimsPrincipal Citizen()
        => ClaimsPrincipalBuilder.Agent("buerger").WithStatus(AgentStatus.Civilian).Build();

    private static PublicTemplateInput Input(string? id = null, string text = Body,
        PublicTemplateKind kind = PublicTemplateKind.TicketAntwort, string title = "Standardantwort",
        bool active = true, int sortOrder = 10)
        => new(id, kind, title, text, active, sortOrder);

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    /// <summary>The service with the audit interceptor attached, as in production: it rewrites Remove into a soft delete.</summary>
    private static (PublicTemplateService Service, SqliteTestContext Context) NewHost()
    {
        var ctx = new SqliteTestContext();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        return (new PublicTemplateService(new TestDbContextFactory(options)), ctx);
    }

    [Fact]
    public async Task Saving_KeepsTheTokensRaw()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        var id = await service.SaveAsync(Input(), Leader());

        var row = await service.GetAsync(id);
        Assert.NotNull(row);
        // the tokens are the payload here; expansion belongs to the moment of applying
        Assert.Contains("BUERGER", row!.Text, StringComparison.Ordinal);
        Assert.Contains("AKTENZEICHEN", row.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForeignTokens_AreRefused()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(Input(text: "Sehr geehrte/r {{Name}}, das ist der falsche Platzhalter."), Leader()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(Input(text: "Lieber BEWERBER, das ist eine Bewerbungs-Vorlage."), Leader()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(Input(text: "Siehe @{Person:11111111-1111-1111-1111-111111111111} in der Akte."),
                Leader()));

        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task TooShortOrTooLong_IsRefused()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Input(text: "Zu kurz"), Leader()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(Input(text: new string('x', PublicTemplateRules.MaxLength + 1)), Leader()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Input(title: "x"), Leader()));
    }

    [Fact]
    public async Task OnlyLeadershipWithWriteAccess_MayEdit()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        foreach (var actor in new[] { Junior(), Supervision(), Partner(), Citizen() })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(Input(), actor));
        }

        var id = await service.SaveAsync(Input(), Leader());
        foreach (var actor in new[] { Junior(), Supervision(), Partner(), Citizen() })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetActiveAsync(id, false, actor));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(id, actor));
        }
    }

    [Fact]
    public async Task GetActive_ReturnsOneKindInOrder()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        await service.SaveAsync(Input(title: "Zweite", sortOrder: 20), Leader());
        await service.SaveAsync(Input(title: "Erste", sortOrder: 10), Leader());
        await service.SaveAsync(Input(title: "Inaktiv", sortOrder: 5, active: false), Leader());
        await service.SaveAsync(Input(title: "Andere Art", kind: PublicTemplateKind.HinweisRueckfrage), Leader());

        var rows = await service.GetActiveAsync(PublicTemplateKind.TicketAntwort);

        Assert.Equal(["Erste", "Zweite"], rows.Select(r => r.Title));
    }

    [Fact]
    public async Task TheAutomaticOne_IsTheFirstActive_OrNothing()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        Assert.Null(await service.GetAutomaticAsync(PublicTemplateKind.TicketEingang));

        await service.SaveAsync(Input(kind: PublicTemplateKind.TicketEingang, title: "Zweite", sortOrder: 20),
            Leader());
        var first = await service.SaveAsync(
            Input(kind: PublicTemplateKind.TicketEingang, title: "Erste", sortOrder: 10), Leader());

        Assert.Equal(first, (await service.GetAutomaticAsync(PublicTemplateKind.TicketEingang))!.Id);

        await service.SetActiveAsync(first, false, Leader());
        Assert.Equal("Zweite", (await service.GetAutomaticAsync(PublicTemplateKind.TicketEingang))!.Title);
    }

    [Fact]
    public async Task Deleting_IsSoftAndDropsOutOfEveryReadPath()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        var id = await service.SaveAsync(Input(), Leader());
        await service.DeleteAsync(id, Leader());

        Assert.Null(await service.GetAsync(id));
        Assert.Empty(await service.GetActiveAsync(PublicTemplateKind.TicketAntwort));
        await using var db = ctx.NewContext();
        Assert.True(await db.OeffentlicheVorlagen.IgnoreQueryFilters().AnyAsync(v => v.Id == id && v.IsDeleted));
    }

    [Fact]
    public async Task TheSeeder_FillsOnlyAnEmptyTableAndItsTextsPassTheOwnRules()
    {
        var (service, ctx) = NewHost();
        using var _ = ctx;

        await using (var db = ctx.NewContext())
        {
            await NOOSE_Website.Infrastructure.PublicTemplateSeeder.SeedAsync(db);
        }

        var seeded = await service.GetAllAsync();
        Assert.Equal(PublicTemplateKindDisplay.All.Count, seeded.Count);
        // one per kind, so both automatic confirmations work out of the box
        foreach (var kind in PublicTemplateKindDisplay.All)
        {
            Assert.NotNull(await service.GetAutomaticAsync(kind));
        }
        // the seeded texts have to survive the same checks a hand-written one does — the seeder adds rows
        // directly, so SaveAsync never sees them
        foreach (var row in seeded)
        {
            Assert.False(PublicTemplateRenderer.HasForeignToken(row.Text));
            Assert.InRange(row.Text.Length, PublicTemplateRules.MinLength, PublicTemplateRules.MaxLength);
            Assert.True(row.Title.Length <= PublicTemplateRules.TitleMaxLength);
            // raw string literals keep whatever indentation the closing delimiter does not strip, and a
            // citizen would read it as a broken letter
            Assert.DoesNotContain(row.Text.ReplaceLineEndings("\n").Split('\n'),
                line => line.StartsWith(' ') || line.StartsWith('\t'));
            Assert.Contains("BUERGER", row.Text, StringComparison.Ordinal);
            Assert.Contains("AKTENZEICHEN", row.Text, StringComparison.Ordinal);
            // an interpolation hole left unfilled would ship "{TicketRules.AgencySender}" to the citizen, and
            // the foreign-token check only looks at doubled braces
            Assert.DoesNotContain("{", row.Text, StringComparison.Ordinal);
        }

        // a deleted template stays deleted: seeding tops up nothing once a row exists
        await service.DeleteAsync(seeded[0].Id, Leader());
        await using (var again = ctx.NewContext())
        {
            await NOOSE_Website.Infrastructure.PublicTemplateSeeder.SeedAsync(again);
        }
        Assert.Equal(seeded.Count - 1, (await service.GetAllAsync()).Count);
    }
}
