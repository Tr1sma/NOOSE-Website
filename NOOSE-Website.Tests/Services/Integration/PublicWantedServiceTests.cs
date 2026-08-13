using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="PublicWantedService"/>: who may publish, and what publishing actually exposes.</summary>
public sealed class PublicWantedServiceTests
{
    private const string PersonId = "p1";

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    /// <summary>Senior Special Agent: may publish directly, is not leadership.</summary>
    private static ClaimsPrincipal Senior()
        => ClaimsPrincipalBuilder.Agent("senior").WithRank(Rank.SeniorSpecialAgent).WithCodename("Kite").Build();

    /// <summary>Rank 2: may prepare a notice, but publishing turns into a request.</summary>
    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.SpecialAgent).WithCodename("Wren").Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static ClaimsPrincipal Citizen()
        => ClaimsPrincipalBuilder.Agent("buerger").WithStatus(AgentStatus.Civilian).Build();

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    private sealed record Host(
        PublicWantedService Service,
        PublicModuleService Modules,
        IFileStorageService PeopleFiles,
        IPublicWantedPhotoStorageService PublicFiles,
        ICaseNumberService CaseNumbers,
        IMemoryCache Cache);

    /// <summary>The service with the audit interceptor attached, as in production.</summary>
    /// <remarks>
    /// The interceptor is what rewrites a <c>Remove</c> into a soft delete, so the recycle-bin tests would exercise a
    /// hard delete without it. <see cref="ICaseNumberService"/> is stubbed: the real one issues MySQL-only raw SQL.
    /// </remarks>
    private static Host NewHost(SqliteTestContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        var factory = new TestDbContextFactory(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var modules = new PublicModuleService(factory, cache);

        var caseNumbers = Substitute.For<ICaseNumberService>();
        caseNumbers.NextAsync(Arg.Any<AppDbContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => $"NOOSE-{ci.ArgAt<string>(1)}-2026-0001");

        var peopleFiles = Substitute.For<IFileStorageService>();
        var publicFiles = Substitute.For<IPublicWantedPhotoStorageService>();

        var service = new PublicWantedService(factory, modules, caseNumbers, peopleFiles, publicFiles,
            Substitute.For<INotificationService>(), cache);
        return new Host(service, modules, peopleFiles, publicFiles, caseNumbers, cache);
    }

    /// <summary>Seeds the module switches plus one clean person file, and turns the wanted module on.</summary>
    private static async Task<SqliteTestContext> SeededAsync(bool moduleOn = true, Action<Person>? person = null)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        if (moduleOn)
        {
            var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Wanted);
            row.IsEnabled = true;
        }
        db.People.Add(Seed.Person(PersonId, "Max Mustermann", p =>
        {
            p.CaseNumber = "NOOSE-P-2026-0001";
            p.WantedReason = "Verdacht auf Waffenhandel";
            p.ThreatScore = 80;
            person?.Invoke(p);
        }));
        await db.SaveChangesAsync();
        return ctx;
    }

    /// <summary>Creates a draft with a publishable accusation and returns its id.</summary>
    private static async Task<string> DraftAsync(Host host, ClaimsPrincipal? actor = null)
    {
        var id = await host.Service.CreateDraftFromPersonAsync(PersonId, actor ?? Leader());
        return id;
    }

    private static async Task<string> PublishedAsync(Host host)
    {
        var id = await DraftAsync(host);
        await host.Service.PublishAsync(id, null, Leader());
        return id;
    }

    private static async Task ClassifyAsync(SqliteTestContext ctx, Action<Person> flag)
    {
        await using var db = ctx.NewContext();
        var person = await db.People.SingleAsync(p => p.Id == PersonId);
        flag(person);
        await db.SaveChangesAsync();
    }

    // ---- the classification gate ----

    [Theory]
    [InlineData(Rank.Director, false)]
    [InlineData(Rank.DeputyDirector, false)]
    [InlineData(Rank.SeniorSpecialAgent, false)]
    [InlineData(Rank.SpecialAgent, true)]
    public async Task Publishing_AClassifiedFile_IsRefusedRegardlessOfRank(Rank rank, bool admin)
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await ClassifyAsync(ctx, p => p.IsClassified = true);

        var builder = ClaimsPrincipalBuilder.Agent("actor").WithRank(rank).WithCodename("X");
        var actor = admin ? builder.AsAdmin().Build() : builder.Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, "Grund", actor));
        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Publishing_WithATruOrHrbFlagOnly_IsRefused(bool tru, bool hrb)
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await ClassifyAsync(ctx, p =>
        {
            p.IsTRUClassified = tru;
            p.IsHRBClassified = hrb;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    [Fact]
    public async Task Publishing_ASoftDeletedFile_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await ClassifyAsync(ctx, p => p.IsDeleted = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    [Fact]
    public async Task Publishing_ForAJuniorAgent_DoesNotRevealThatTheFileIsClassified()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await ClassifyAsync(ctx, p => p.IsClassified = true);

        var seen = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.PublishAsync(id, "Grund", Junior()));
        var known = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.PublishAsync(id, null, Leader()));

        Assert.DoesNotContain("Verschlusssache", seen.Message);
        Assert.Contains("Verschlusssache", known.Message);
    }

    [Fact]
    public async Task Publishing_ByTheReadOnlySupervision_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.PublishAsync(id, null, OnlyReader()));
    }

    [Fact]
    public async Task Publishing_ByACitizenAccount_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.PublishAsync(id, "Grund", Citizen()));
    }

    [Fact]
    public async Task Publishing_WhileTheModuleIsOff_IsRefused()
    {
        using var ctx = await SeededAsync(moduleOn: false);
        var host = NewHost(ctx);
        var id = await DraftAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    [Fact]
    public async Task Publishing_WhileTheKillSwitchIsOn_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await host.Modules.KillSwitchSetAsync(true,
            ClaimsPrincipalBuilder.Agent("admin").WithRank(Rank.Director).AsAdmin().Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    // ---- the rank switch ----

    [Fact]
    public async Task Publishing_AtRankThree_GoesLiveDirectly()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Senior());

        var outcome = await host.Service.PublishAsync(id, null, Senior());

        Assert.Equal(PublicWantedPublishOutcome.Published, outcome);
        var card = Assert.Single((await host.Service.GetBoardAsync()).Cards);
        Assert.Equal("NOOSE-FA-2026-0001", card.CaseNumber);
    }

    [Fact]
    public async Task Publishing_AtRankTwo_CreatesARequestAndLeavesTheEntryBeantragt()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Junior());

        var outcome = await host.Service.PublishAsync(id, "Bitte ausschreiben", Junior());

        Assert.Equal(PublicWantedPublishOutcome.Requested, outcome);
        Assert.Empty((await host.Service.GetBoardAsync()).Cards);

        await using var db = ctx.NewContext();
        var request = await db.Requests.SingleAsync();
        Assert.Equal(RequestType.Veroeffentlichung, request.Type);
        Assert.Equal(id, request.PublicationWantedId);
        Assert.Equal(PublicWantedStatus.Beantragt, (await db.OeffentlicheFahndungen.SingleAsync()).Status);
    }

    [Fact]
    public async Task Publishing_AtRankTwo_WithoutAJustification_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Junior());

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, "  ", Junior()));
    }

    [Fact]
    public async Task Publishing_AtRankTwo_TwiceIsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Junior());
        await host.Service.PublishAsync(id, "Bitte ausschreiben", Junior());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.PublishAsync(id, "Nochmal", Junior()));
    }

    [Fact]
    public async Task ThePublicationRequest_CarriesNoAccusationHtml()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Junior());
        await host.Service.PublishAsync(id, "Bitte ausschreiben", Junior());

        await using var db = ctx.NewContext();
        var request = await db.Requests.SingleAsync();
        Assert.DoesNotContain("<p>", request.Justification ?? string.Empty);
        Assert.DoesNotContain("Waffenhandel", request.TargetDesignation);
    }

    // ---- snapshot isolation, the point of the phase ----

    [Fact]
    public async Task RenamingThePersonAfterPublication_DoesNotChangeTheBoard()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);

        await using (var db = ctx.NewContext())
        {
            var person = await db.People.SingleAsync(p => p.Id == PersonId);
            person.Name = "Ganz anderer Name";
            person.WantedReason = "Ganz anderer Vorwurf";
            await db.SaveChangesAsync();
        }
        DropCache(host);

        var card = Assert.Single((await host.Service.GetBoardAsync()).Cards);
        Assert.Equal("Max Mustermann", card.DisplayName);
        var detail = await host.Service.GetByCaseNumberAsync(card.CaseNumber);
        Assert.Contains("Waffenhandel", detail!.ChargeHtml);
    }

    [Fact]
    public async Task ChangingTheThreatScore_DoesNotChangeThePublishedHazardLevel()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);
        var before = Assert.Single((await host.Service.GetBoardAsync()).Cards).HazardLevel;

        await using (var db = ctx.NewContext())
        {
            var person = await db.People.SingleAsync(p => p.Id == PersonId);
            person.ThreatScore = 1;
            await db.SaveChangesAsync();
        }
        DropCache(host);

        Assert.Equal(before, Assert.Single((await host.Service.GetBoardAsync()).Cards).HazardLevel);
        Assert.Equal(HazardLevel.Critical, before);
    }

    [Fact]
    public async Task CreateDraft_CopiesOnlyNameAndAccusation()
    {
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            db.PersonAliases.Add(new PersonAlias { PersonId = PersonId, AliasName = "Schatten" });
            db.PersonVehicles.Add(new PersonVehicle { PersonId = PersonId, Designation = "Sultan", LicensePlate = "AB123" });
            db.PersonLocations.Add(new PersonLocation { PersonId = PersonId, Text = "Sandy Shores" });
            await db.SaveChangesAsync();
        }
        var host = NewHost(ctx);

        var id = await DraftAsync(host);
        var draft = await host.Service.GetDraftAsync(id, Leader());

        Assert.Equal("Max Mustermann", draft!.DisplayName);
        Assert.Contains("Waffenhandel", draft.ChargeHtml);
        Assert.Null(draft.AliasText);
        Assert.Null(draft.VehicleText);
        Assert.Null(draft.LastArea);
        Assert.Null(draft.PhotoSourceId);
    }

    [Fact]
    public async Task CreateDraft_ForAClassifiedFile_IsRefused()
    {
        using var ctx = await SeededAsync(person: p => p.IsClassified = true);
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.CreateDraftFromPersonAsync(PersonId, Leader()));
    }

    [Fact]
    public async Task CreateDraft_ForAFileTheActorCannotSee_ReadsAsNotFound()
    {
        using var ctx = await SeededAsync(person: p => p.IsClassified = true);
        var host = NewHost(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.CreateDraftFromPersonAsync(PersonId, Junior()));
        Assert.DoesNotContain("Verschlusssache", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_Twice_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await DraftAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.CreateDraftFromPersonAsync(PersonId, Leader()));
    }

    [Fact]
    public async Task CreateDraft_WorksWhileTheModuleIsOff()
    {
        using var ctx = await SeededAsync(moduleOn: false);
        var host = NewHost(ctx);

        var id = await DraftAsync(host);

        Assert.NotNull(await host.Service.GetDraftAsync(id, Leader()));
    }

    // ---- a file that turns classified or vanishes after publication ----

    [Fact]
    public async Task AFileClassifiedAfterPublication_DisappearsFromTheBoard()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        // straight through the context: proves the read-side belt on its own, without the retract hook
        await ClassifyAsync(ctx, p => p.IsClassified = true);
        DropCache(host);

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
        Assert.Null(await host.Service.GetByCaseNumberAsync("NOOSE-FA-2026-0001"));
        Assert.Null(await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001"));
        // the row itself is untouched — the belt subtracts, it does not rewrite
        await using var db = ctx.NewContext();
        Assert.Equal(PublicWantedStatus.Veroeffentlicht, (await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id)).Status);
    }

    [Fact]
    public async Task AFileSoftDeletedAfterPublication_DisappearsFromTheBoard()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);
        await ClassifyAsync(ctx, p => p.IsDeleted = true);
        DropCache(host);

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
    }

    [Fact]
    public async Task RetractForRecord_TakesEveryLiveNoticeOffline()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await host.Service.RetractForRecordAsync(PersonId, "Akte als Verschlusssache eingestuft.", Leader());

        await using var db = ctx.NewContext();
        var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
        Assert.Equal(PublicWantedStatus.Zurueckgezogen, row.Status);
        Assert.Equal("Akte als Verschlusssache eingestuft.", row.RetractedReason);
        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
    }

    [Fact]
    public async Task RetractForRecord_AlsoClosesAnOpenPublicationRequest()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Junior());
        await host.Service.PublishAsync(id, "Bitte ausschreiben", Junior());

        await host.Service.RetractForRecordAsync(PersonId, "Akte gelöscht.", Leader());

        await using var db = ctx.NewContext();
        Assert.Equal(RequestStatus.Rejected, (await db.Requests.SingleAsync()).Status);
    }

    [Fact]
    public async Task ASoftDeletedNotice_NeverReachesTheBoard()
    {
        // the suppression belt reads the person file with IgnoreQueryFilters; that flag is compilation-scoped, so a
        // subquery using it would strip !IsDeleted from the notice set as well and serve a deleted poster
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        DropCache(host);

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
        Assert.Null(await host.Service.GetByCaseNumberAsync("NOOSE-FA-2026-0001"));
        Assert.Null(await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001"));
    }

    [Fact]
    public async Task ANoticeWhosePersonRowIsGone_IsSuppressed()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);
        await using (var db = ctx.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Personen WHERE Id = {0}", PersonId);
        }
        DropCache(host);

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
    }

    // ---- the notice carries the file's content, so it answers to the file's read gate ----

    [Fact]
    public async Task GetAllAsync_HidesANoticeWhoseFileTheActorMayNotRead()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await DraftAsync(host);
        await ClassifyAsync(ctx, p =>
        {
            p.IsClassified = true;
            p.IsTRUClassified = true;
        });

        Assert.Empty(await host.Service.GetAllAsync(Senior()));
        Assert.Single(await host.Service.GetAllAsync(Leader()));
    }

    [Fact]
    public async Task GetDraftAndOptions_ForAFileTheActorMayNotRead_AreEmpty()
    {
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            db.PersonLocations.Add(new PersonLocation { PersonId = PersonId, Text = "Sandy Shores" });
            await db.SaveChangesAsync();
        }
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await ClassifyAsync(ctx, p =>
        {
            p.IsClassified = true;
            p.IsTRUClassified = true;
        });

        // these are live rows of the file, not snapshot fields
        Assert.Null(await host.Service.GetDraftAsync(id, Senior()));
        Assert.Empty((await host.Service.GetOptionsAsync(id, Senior())).Areas);

        Assert.NotNull(await host.Service.GetDraftAsync(id, Leader()));
        Assert.Single((await host.Service.GetOptionsAsync(id, Leader())).Areas);
    }

    [Fact]
    public async Task GetForPersonAsync_ForAFileTheActorMayNotRead_IsNull()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await DraftAsync(host);
        await ClassifyAsync(ctx, p =>
        {
            p.IsClassified = true;
            p.IsTRUClassified = true;
        });

        Assert.Null(await host.Service.GetForPersonAsync(PersonId, Senior()));
        Assert.NotNull(await host.Service.GetForPersonAsync(PersonId, Leader()));
    }

    [Fact]
    public async Task ARankTwoAgent_CanOpenTheNoticeHePrepared()
    {
        // without this the rank switch is dead: he creates a draft in the file and can never reach the request
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host, Junior());

        Assert.NotNull(await host.Service.GetForPersonAsync(PersonId, Junior()));
        Assert.NotNull(await host.Service.GetDraftAsync(id, Junior()));
        await host.Service.GetOptionsAsync(id, Junior());
        // the cross-record list stays at rank 3
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAllAsync(Junior()));
    }

    [Fact]
    public async Task APartnerAndACitizen_CannotOpenANoticeAtAll()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        var partner = ClaimsPrincipalBuilder.Agent("partner").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetDraftAsync(id, partner));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetForPersonAsync(PersonId, Citizen()));
    }

    // ---- everything that is not published looks the same from outside ----

    [Theory]
    [InlineData(PublicWantedStatus.Entwurf)]
    [InlineData(PublicWantedStatus.Beantragt)]
    [InlineData(PublicWantedStatus.Zurueckgezogen)]
    [InlineData(PublicWantedStatus.Gefasst)]
    [InlineData(PublicWantedStatus.Abgelaufen)]
    public async Task Detail_ForEveryNonPublicState_IsIndistinguishable(PublicWantedStatus status)
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
            row.Status = status;
            await db.SaveChangesAsync();
        }
        DropCache(host);

        Assert.Null(await host.Service.GetByCaseNumberAsync("NOOSE-FA-2026-0001"));
        Assert.Null(await host.Service.GetByCaseNumberAsync("NOOSE-FA-9999-0001"));
    }

    [Fact]
    public async Task Detail_MatchesTheCaseNumberCaseInsensitively()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);

        Assert.NotNull(await host.Service.GetByCaseNumberAsync("noose-fa-2026-0001"));
    }

    [Fact]
    public async Task AnExpiredEntry_LeavesTheBoardWithoutAWorker()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        DropCache(host);

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
    }

    [Fact]
    public async Task TheBoard_IsEmptyWhileTheModuleIsOff()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await PublishedAsync(host);
        await host.Modules.SaveAsync(
            [new PublicModuleInput { Key = PublicModules.Wanted, IsEnabled = false }],
            ClaimsPrincipalBuilder.Agent("admin").WithRank(Rank.Director).AsAdmin().Build());

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
        Assert.Null(await host.Service.GetByCaseNumberAsync("NOOSE-FA-2026-0001"));
    }

    // ---- the accusation text ----

    [Fact]
    public async Task Publishing_AnAccusationContainingAMention_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await SetChargeAsync(ctx, id, $"<p>Kontakt zu {MentionParser.Token("Person", Guid.NewGuid().ToString())}</p>");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    [Fact]
    public async Task Publishing_AnAccusationContainingAPlaceholder_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await SetChargeAsync(ctx, id, "<p>Gesucht wegen {{Aktenzeichen}}</p>");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    [Fact]
    public async Task Publishing_AnEmptyAccusation_IsRefused()
    {
        using var ctx = await SeededAsync(person: p => p.WantedReason = null);
        var host = NewHost(ctx);
        var id = await DraftAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.PublishAsync(id, null, Leader()));
    }

    [Fact]
    public async Task AnAccusationThatIsOnlyAnImage_IsPublishable()
    {
        using var ctx = await SeededAsync(person: p => p.WantedReason = null);
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await SetChargeAsync(ctx, id, "<p><img src=\"data:image/png;base64,iVBORw0KGgo=\" /></p>");

        await host.Service.PublishAsync(id, null, Leader());

        Assert.Single((await host.Service.GetBoardAsync()).Cards);
    }

    [Fact]
    public async Task Publishing_ReCleansTheStoredAccusation_EvenWhenItWasWrittenAroundTheService()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await SetChargeAsync(ctx, id, "<p>Gefährlich</p><script>alert(1)</script>");

        await host.Service.PublishAsync(id, null, Leader());

        var detail = await host.Service.GetByCaseNumberAsync("NOOSE-FA-2026-0001");
        Assert.DoesNotContain("<script", detail!.ChargeHtml, StringComparison.OrdinalIgnoreCase);
    }

    // ---- lifecycle ----

    [Fact]
    public async Task Retracting_KeepsTheCaseNumberAndTheAccusation()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await host.Service.RetractAsync(id, "Hinweis war falsch", Leader());

        var draft = await host.Service.GetDraftAsync(id, Leader());
        Assert.Equal("NOOSE-FA-2026-0001", draft!.CaseNumber);
        Assert.Contains("Waffenhandel", draft.ChargeHtml);
        Assert.Equal(PublicWantedStatus.Zurueckgezogen, draft.Status);
    }

    [Fact]
    public async Task Retracting_RequiresAReason()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.RetractAsync(id, "   ", Leader()));
    }

    [Fact]
    public async Task Retracting_WorksWhileTheModuleIsOff()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await host.Modules.KillSwitchSetAsync(true,
            ClaimsPrincipalBuilder.Agent("admin").WithRank(Rank.Director).AsAdmin().Build());

        await host.Service.RetractAsync(id, "Not-Aus, trotzdem sauber schließen", Leader());

        await using var db = ctx.NewContext();
        Assert.Equal(PublicWantedStatus.Zurueckgezogen, (await db.OeffentlicheFahndungen.SingleAsync()).Status);
    }

    [Fact]
    public async Task Captured_DropsTheEntryFromTheBoard()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await host.Service.CapturedAsync(id, Leader());

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
        await using var db = ctx.NewContext();
        var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
        Assert.Equal(PublicWantedStatus.Gefasst, row.Status);
        Assert.NotNull(row.CapturedAt);
    }

    [Fact]
    public async Task Delete_WhilePublished_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.DeleteAsync(id, Leader()));
    }

    [Fact]
    public async Task Delete_ThenRestore_BringsTheEntryBackAsADraft()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await host.Service.RetractAsync(id, "Erledigt", Leader());
        await host.Service.DeleteAsync(id, Leader());

        var trash = await host.Service.GetTrashAsync();
        Assert.Equal(id, Assert.Single(trash).Id);

        await host.Service.RestoreAsync(id, Leader());

        var draft = await host.Service.GetDraftAsync(id, Leader());
        Assert.Equal(PublicWantedStatus.Entwurf, draft!.Status);
        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
    }

    [Fact]
    public async Task Restore_AfterTheFileBecameClassified_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await DraftAsync(host);
        await host.Service.DeleteAsync(id, Leader());
        await ClassifyAsync(ctx, p => p.IsClassified = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.RestoreAsync(id, Leader()));
    }

    [Fact]
    public async Task Publishing_MintsTheCaseNumberOnlyOnce()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await host.Service.RetractAsync(id, "Kurz offline", Leader());

        await host.Service.PublishAsync(id, null, Leader());

        var card = Assert.Single((await host.Service.GetBoardAsync()).Cards);
        Assert.Equal("NOOSE-FA-2026-0001", card.CaseNumber);
        // the substitute answers with a constant, so only the call count can tell ??= from =
        await host.CaseNumbers.Received(1)
            .NextAsync(Arg.Any<AppDbContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExpiryPickedInTheEditor_LastsToTheEndOfThatDayLocally()
    {
        // MudDatePicker hands over local midnight; compared against UtcNow raw, the notice would drop off early
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        var today = DateTime.Now.Date;

        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Max Mustermann", ExpiresAt = today }, Leader());

        Assert.Single((await host.Service.GetBoardAsync()).Cards);
        await using var db = ctx.NewContext();
        var stored = (await db.OeffentlicheFahndungen.SingleAsync()).ExpiresAt;
        Assert.NotNull(stored);
        Assert.True(stored > DateTime.UtcNow, "Ein heute gewähltes Ablaufdatum gilt bis zum Tagesende.");
    }

    // ---- the photo copy ----

    [Fact]
    public async Task Publishing_CopiesTheFilePhoto_AndNeverStoresTheInternalFileName()
    {
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            db.PersonPhotos.Add(new PersonPhoto
            {
                Id = "foto-1",
                PersonId = PersonId,
                FileNameSaved = "intern.jpg",
                OriginalName = "o.jpg",
                ContentType = "image/jpeg",
            });
            await db.SaveChangesAsync();
        }
        var host = NewHost(ctx);
        host.PeopleFiles.OpenRead("intern.jpg").Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("bild")));
        host.PublicFiles.SaveAsync(Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>())
            .Returns("oeffentlich.jpg");

        var id = await DraftAsync(host);
        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Max Mustermann", PhotoSourceId = "foto-1" }, Leader());
        await host.Service.PublishAsync(id, null, Leader());

        var photo = await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001");
        Assert.Equal("oeffentlich.jpg", photo!.FileNameSaved);
        Assert.Equal("image/jpeg", photo.ContentType);
        Assert.NotEqual("intern.jpg", photo.FileNameSaved);
    }

    [Fact]
    public async Task ClearingThePhotoOfALiveNotice_TakesTheCopyOffline()
    {
        // the copy used to follow the choice only at publish time, so removing a mugshot reported success while it
        // stayed anonymously downloadable
        using var ctx = await SeededAsync();
        var host = await PhotoHostAsync(ctx);
        var id = await DraftAsync(host);
        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Max Mustermann", PhotoSourceId = "foto-1" }, Leader());
        await host.Service.PublishAsync(id, null, Leader());
        Assert.NotNull(await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001"));

        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Max Mustermann", PhotoSourceId = null }, Leader());

        Assert.Null(await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001"));
        Assert.False(Assert.Single((await host.Service.GetBoardAsync()).Cards).HasPhoto);
        host.PublicFiles.Received().Delete("oeffentlich.jpg");
    }

    [Fact]
    public async Task SwappingThePhotoOfALiveNotice_ReplacesTheCopy()
    {
        using var ctx = await SeededAsync();
        var host = await PhotoHostAsync(ctx);
        host.PublicFiles.SaveAsync(Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>())
            .Returns("zweite.png");
        var id = await DraftAsync(host);
        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Max Mustermann", PhotoSourceId = "foto-1" }, Leader());
        await host.Service.PublishAsync(id, null, Leader());

        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Max Mustermann", PhotoSourceId = "foto-2" }, Leader());

        var photo = await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001");
        Assert.Equal("zweite.png", photo!.FileNameSaved);
        host.PublicFiles.Received().Delete("oeffentlich.jpg");
    }

    [Fact]
    public async Task GetPublishedPhoto_ForADraftOrAnUnknownCaseNumber_IsNull()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await DraftAsync(host);

        Assert.Null(await host.Service.GetPublishedPhotoAsync("NOOSE-FA-2026-0001"));
        Assert.Null(await host.Service.GetPublishedPhotoAsync("gibt-es-nicht"));
        Assert.Null(await host.Service.GetPublishedPhotoAsync(null));
    }

    // ---- caching ----

    [Fact]
    public async Task TheBoard_IsCachedAndDroppedOnEveryWrite()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        Assert.Single((await host.Service.GetBoardAsync()).Cards);

        // a write around the service is not seen — that is the cache doing its job
        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
            row.DisplayName = "Direkt geändert";
            await db.SaveChangesAsync();
        }
        Assert.Equal("Max Mustermann", Assert.Single((await host.Service.GetBoardAsync()).Cards).DisplayName);

        // a write through the service invalidates it immediately
        await host.Service.UpdateSnapshotAsync(
            new PublicWantedInput { Id = id, DisplayName = "Über den Dienst" }, Leader());
        Assert.Equal("Über den Dienst", Assert.Single((await host.Service.GetBoardAsync()).Cards).DisplayName);
    }

    // ---- guards on the internal read paths ----

    [Fact]
    public async Task GetAllAsync_AdmitsRankThreeAndTheSupervision_ButNotAJuniorAgent()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await DraftAsync(host);

        Assert.Single(await host.Service.GetAllAsync(Senior()));
        Assert.Single(await host.Service.GetAllAsync(OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAllAsync(Junior()));
    }

    [Fact]
    public async Task GetAllAsync_ProjectsTheCodename_NotTheIdentityUser()
    {
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent("lead", configure: a =>
            {
                a.Codename = "Falcon";
                a.RealName = "Klarname Klar";
            }));
            await db.SaveChangesAsync();
        }
        var host = NewHost(ctx);
        await PublishedAsync(host);

        var row = Assert.Single(await host.Service.GetAllAsync(Leader()));
        Assert.Equal("Falcon", row.PublishedByName);
    }

    // ---- deciding a publication request ----

    private static async Task<(string WantedId, string RequestId)> RequestedAsync(Host host, SqliteTestContext ctx)
    {
        var id = await DraftAsync(host, Junior());
        await host.Service.PublishAsync(id, "Bitte ausschreiben", Junior());
        await using var db = ctx.NewContext();
        return (id, (await db.Requests.SingleAsync()).Id);
    }

    [Fact]
    public async Task Approving_PublishesThroughTheSameBodyAsADirectPublish()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (wantedId, requestId) = await RequestedAsync(host, ctx);

        await host.Service.ApprovePublicationRequestAsync(requestId, "Passt", Leader());

        var card = Assert.Single((await host.Service.GetBoardAsync()).Cards);
        Assert.Equal("NOOSE-FA-2026-0001", card.CaseNumber);
        await using var db = ctx.NewContext();
        Assert.Equal(RequestStatus.Approved, (await db.Requests.SingleAsync()).Status);
        Assert.Equal(PublicWantedStatus.Veroeffentlicht,
            (await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == wantedId)).Status);
    }

    [Fact]
    public async Task Approving_AfterTheFileBecameClassified_IsRefusedAndTheRowStaysBeantragt()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (wantedId, requestId) = await RequestedAsync(host, ctx);
        await ClassifyAsync(ctx, p => p.IsClassified = true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ApprovePublicationRequestAsync(requestId, null, Leader()));

        await using var db = ctx.NewContext();
        Assert.Equal(RequestStatus.Requested, (await db.Requests.SingleAsync()).Status);
        Assert.Equal(PublicWantedStatus.Beantragt,
            (await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == wantedId)).Status);
    }

    [Fact]
    public async Task Approving_IntoASwitchedOffModule_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (_, requestId) = await RequestedAsync(host, ctx);
        await host.Modules.KillSwitchSetAsync(true,
            ClaimsPrincipalBuilder.Agent("admin").WithRank(Rank.Director).AsAdmin().Build());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ApprovePublicationRequestAsync(requestId, null, Leader()));
    }

    [Fact]
    public async Task Approving_BelowRankThree_IsRefused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (_, requestId) = await RequestedAsync(host, ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.ApprovePublicationRequestAsync(requestId, null, Junior()));
    }

    [Fact]
    public async Task Rejecting_LeavesTheEntryAsADraft()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (wantedId, requestId) = await RequestedAsync(host, ctx);

        await host.Service.RejectPublicationRequestAsync(requestId, "Nicht jetzt", Leader());

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
        var draft = await host.Service.GetDraftAsync(wantedId, Leader());
        Assert.Equal(PublicWantedStatus.Entwurf, draft!.Status);
    }

    [Fact]
    public async Task Approving_ContentThatBecameUnpublishable_IsRefused_WithoutCopyingAPhoto()
    {
        // the content rules live in the shared publish body, not only in PublishAsync — a Beantragt row can be edited
        // between filing and decision, and the copy must not be written before the refusal
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (wantedId, requestId) = await RequestedAsync(host, ctx);
        await SetChargeAsync(ctx, wantedId, "<p>Gesucht wegen {{Aktenzeichen}}</p>");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ApprovePublicationRequestAsync(requestId, null, Leader()));

        Assert.Empty((await host.Service.GetBoardAsync()).Cards);
        await host.PublicFiles.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approving_ByTheReadOnlySupervision_IsRefusedBeforeAnythingIsWritten()
    {
        // RequireHighestClassification alone admits the supervision, and the page that renders the button does too;
        // without the write guard the case number and the photo copy happen before the interceptor vetoes the save
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (_, requestId) = await RequestedAsync(host, ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.ApprovePublicationRequestAsync(requestId, null, OnlyReader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.RejectPublicationRequestAsync(requestId, null, OnlyReader()));
        await host.PublicFiles.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deleting_ABeantragtNotice_ClosesItsRequest()
    {
        // otherwise the badge counts a request the inbox cannot show and nobody can decide
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (wantedId, requestId) = await RequestedAsync(host, ctx);

        await host.Service.DeleteAsync(wantedId, Leader());

        Assert.Equal(0, await host.Service.GetPendingRequestCountAsync());
        Assert.Empty(await host.Service.GetPendingRequestsAsync());
        await using var db = ctx.NewContext();
        Assert.Equal(RequestStatus.Rejected, (await db.Requests.SingleAsync(a => a.Id == requestId)).Status);
    }

    [Fact]
    public async Task ThePendingCountAndTheInbox_AreBuiltFromTheSameJoin()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (wantedId, _) = await RequestedAsync(host, ctx);
        // a notice that vanished around the service must not leave the badge counting
        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == wantedId);
            row.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        Assert.Equal((await host.Service.GetPendingRequestsAsync()).Count,
            await host.Service.GetPendingRequestCountAsync());
    }

    [Fact]
    public async Task GetPendingRequests_CountAndRowsCoverOnlyOpenPublicationRequests()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var (_, requestId) = await RequestedAsync(host, ctx);

        Assert.Equal(1, await host.Service.GetPendingRequestCountAsync());
        var row = Assert.Single(await host.Service.GetPendingRequestsAsync());
        Assert.Equal(requestId, row.RequestId);
        Assert.Equal("Max Mustermann", row.DisplayName);

        await host.Service.RejectPublicationRequestAsync(requestId, null, Leader());
        Assert.Equal(0, await host.Service.GetPendingRequestCountAsync());
    }

    // ---- audit ----

    [Fact]
    public async Task PublishAndRetract_AreAuditedByTheInterceptor_WithoutAManualRow()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await PublishedAsync(host);
        await host.Service.RetractAsync(id, "Erledigt", Leader());

        await using var db = ctx.NewContext();
        var rows = await db.AuditLogs.Where(a => a.EntityType == nameof(OeffentlicheFahndung)).ToListAsync();
        Assert.Contains(rows, a => a.Action == AuditAction.Created);
        Assert.Contains(rows, a => a.Action == AuditAction.Modified);
        // nothing is logged against the person file itself; the timeline fans in over the snapshot type
        Assert.Empty(await db.AuditLogs.Where(a => a.EntityType == nameof(Person)).ToListAsync());
    }

    /// <summary>A host whose person file carries two photos and whose storage substitutes answer.</summary>
    private static async Task<Host> PhotoHostAsync(SqliteTestContext ctx)
    {
        await using (var db = ctx.NewContext())
        {
            db.PersonPhotos.Add(new PersonPhoto
            {
                Id = "foto-1", PersonId = PersonId, FileNameSaved = "intern.jpg",
                OriginalName = "o.jpg", ContentType = "image/jpeg",
            });
            db.PersonPhotos.Add(new PersonPhoto
            {
                Id = "foto-2", PersonId = PersonId, FileNameSaved = "intern2.png",
                OriginalName = "o2.png", ContentType = "image/png",
            });
            await db.SaveChangesAsync();
        }

        var host = NewHost(ctx);
        host.PeopleFiles.OpenRead(Arg.Any<string>()).Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("bild")));
        host.PublicFiles.SaveAsync(Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>())
            .Returns("oeffentlich.jpg");
        return host;
    }

    private static async Task SetChargeAsync(SqliteTestContext ctx, string id, string html)
    {
        await using var db = ctx.NewContext();
        var row = await db.OeffentlicheFahndungen.SingleAsync(f => f.Id == id);
        row.ChargeHtml = html;
        await db.SaveChangesAsync();
    }

    /// <summary>Drops the 10-second snapshot; a test that changes the database around the service has to say so.</summary>
    private static void DropCache(Host host) => host.Cache.Remove("OeffentlicheFahndungen");
}
