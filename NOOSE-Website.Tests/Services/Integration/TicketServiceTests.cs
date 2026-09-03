using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="TicketService"/>: who may open, who may answer, and what stays inside.</summary>
public sealed class TicketServiceTests
{
    private const string CitizenUserId = "buerger1";
    private const string ProfileId = "profil1";
    private const string OtherUserId = "buerger2";
    private const string OtherProfileId = "profil2";

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal Admin()
        => ClaimsPrincipalBuilder.Agent("admin").WithRank(Rank.Director).AsAdmin().Build();

    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.SpecialAgent).WithCodename("Wren").Build();

    private static ClaimsPrincipal Supervision()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).WithCodename("Owl")
            .AsTeamLead().Build();

    private static ClaimsPrincipal Partner()
        => ClaimsPrincipalBuilder.Agent("partner").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

    private static ClaimsPrincipal Citizen(string id = CitizenUserId)
        => ClaimsPrincipalBuilder.Agent(id).WithStatus(AgentStatus.Civilian).Build();

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    private sealed record Host(
        TicketService Service,
        PublicModuleService Modules,
        INotificationService Notifications,
        IDiscordWebhookService Discord,
        TestDbContextFactory Factory);

    /// <summary>The service with the audit interceptor attached, as in production.</summary>
    /// <remarks>
    /// The interceptor rewrites <c>Remove</c> into a soft delete, which the quota and trash facts depend on.
    /// <see cref="ICaseNumberService"/> is stubbed — the real one issues MySQL-only raw SQL — but it counts up, or
    /// the unique index on the case number would fail on the second ticket.
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

        var seq = 0;
        var caseNumbers = Substitute.For<ICaseNumberService>();
        caseNumbers.NextAsync(Arg.Any<AppDbContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => $"NOOSE-{ci.ArgAt<string>(1)}-2026-{++seq:0000}");

        var notifications = Substitute.For<INotificationService>();
        var discord = Substitute.For<IDiscordWebhookService>();
        var service = new TicketService(factory, modules, new BuergerService(factory), caseNumbers,
            notifications, new PublicTemplateService(factory), discord, new TicketBroadcaster());
        return new Host(service, modules, notifications, discord, factory);
    }

    /// <summary>Seeds the module switch and two complete citizen profiles.</summary>
    private static async Task<SqliteTestContext> SeededAsync(
        bool ticketsOn = true, Action<BuergerProfil>? profile = null)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Tickets);
        row.IsEnabled = ticketsOn;

        db.Users.Add(Seed.Agent("lead", Rank.Director, configure: a => a.Codename = "Falcon"));
        // an ordinary agent, so the participant picker predicate has someone to find
        db.Users.Add(Seed.Agent("junior", Rank.SpecialAgent, configure: a => a.Codename = "Wren"));

        var mine = new BuergerProfil
        {
            Id = ProfileId,
            UserId = CitizenUserId,
            FirstName = "Erika",
            LastName = "Musterfrau",
        };
        profile?.Invoke(mine);
        db.BuergerProfile.Add(mine);
        db.BuergerProfile.Add(new BuergerProfil
        {
            Id = OtherProfileId,
            UserId = OtherUserId,
            FirstName = "Klaus",
            LastName = "Kleber",
        });
        await db.SaveChangesAsync();
        return ctx;
    }

    private static Task<string> OpenAsync(Host host, string? subject = null, string? text = null,
        ClaimsPrincipal? actor = null)
        => host.Service.OpenAsync(
            new TicketInput
            {
                Subject = subject ?? "Frage zu meinem Fahrzeug",
                Text = text ?? "Mein Kennzeichen steht auf der Fahndung, obwohl das Fahrzeug verkauft wurde.",
            },
            actor ?? Citizen());

    private static async Task<string> IdAsync(Host host, string caseNumber)
    {
        await using var db = host.Factory.CreateDbContext();
        return await db.Tickets.Where(t => t.CaseNumber == caseNumber).Select(t => t.Id).SingleAsync();
    }

    // ---- opening ----

    [Fact]
    public async Task Opening_mints_a_case_number_and_stores_the_first_message()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        var caseNumber = await OpenAsync(host);

        Assert.StartsWith("NOOSE-T-", caseNumber, StringComparison.Ordinal);
        await using var db = ctx.NewContext();
        var row = await db.Tickets.SingleAsync();
        Assert.Equal(ProfileId, row.CitizenProfileId);
        Assert.Equal(TicketStatus.Offen, row.Status);
        Assert.Equal(TicketArt.Fuehrungsebene, row.Kind);
        var message = await db.TicketNachrichten.SingleAsync();
        Assert.Equal(TicketMessageAudience.Buerger, message.Audience);
        Assert.True(message.AuthorIsCitizen);
        Assert.Null(message.AuthorAgentId);
    }

    [Fact]
    public async Task Opening_pings_the_leadership_role_on_Discord_without_naming_the_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);

        // no headline argument: the channel gets the generic notice plus the link, never subject or name
        await host.Discord.Received(1).PushAsync(NotificationType.PublicTicketCreated, $"/tickets/{id}",
            null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_citizen_reply_pings_nobody_on_Discord()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        host.Discord.ClearReceivedCalls();

        await host.Service.ReplyAsCitizenAsync(caseNumber, "Ich habe den Kaufvertrag gefunden.", Citizen());

        // only the opening is an event for the channel; a running thread would turn the ping into noise
        await host.Discord.DidNotReceive().PushAsync(Arg.Any<NotificationType>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_short_subject_or_text_is_refused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(host, subject: "Hi"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(host, text: "zu kurz"));
    }

    [Fact]
    public async Task A_closed_module_refuses_the_opening()
    {
        using var ctx = await SeededAsync(ticketsOn: false);
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(host));
    }

    [Fact]
    public async Task A_closed_module_leaves_a_running_ticket_readable_and_answerable()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);

        // the switch stops new concerns; it does not strand a conversation the agency itself started
        await host.Modules.SaveAsync(
            [new PublicModuleInput { Key = PublicModules.Tickets, IsEnabled = false }], Admin());

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        Assert.NotNull(detail);
        await host.Service.ReplyAsCitizenAsync(caseNumber, "Ich habe den Kaufvertrag gefunden.", Citizen());
        Assert.Equal(2, (await host.Service.GetOwnDetailAsync(caseNumber, Citizen()))!.Messages.Count);
    }

    [Fact]
    public async Task An_account_without_a_profile_cannot_open_one()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(host, actor: Citizen("fremd")));
    }

    [Fact]
    public async Task A_blocked_account_cannot_open_one()
    {
        using var ctx = await SeededAsync(profile: p =>
        {
            p.IsBlocked = true;
            p.BlockedReason = "Missbrauch";
        });
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => OpenAsync(host));
    }

    [Fact]
    public async Task The_open_cap_refuses_the_next_ticket_and_closing_one_frees_the_slot()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var first = await OpenAsync(host);
        await OpenAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(host));

        await host.Service.SetStatusAsync(await IdAsync(host, first), TicketStatus.Geschlossen, Leader());
        // the daily cap is 3, so exactly one more fits through
        var third = await OpenAsync(host);
        Assert.StartsWith("NOOSE-T-", third, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_daily_cap_counts_deleted_tickets_too()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        for (var i = 0; i < TicketRules.PerDay; i++)
        {
            var caseNumber = await OpenAsync(host);
            var id = await IdAsync(host, caseNumber);
            // closing frees the open cap, deleting must not free the daily one
            await host.Service.SetStatusAsync(id, TicketStatus.Geschlossen, Leader());
            await host.Service.DeleteAsync(id, Leader());
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(host));
    }

    // ---- who may look ----

    [Fact]
    public async Task A_junior_agent_a_citizen_and_a_partner_are_refused_at_the_desk()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Junior()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Citizen()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Partner()));

        // not a refusal any more: an agent who is not attached gets "does not exist", because a refusal would
        // confirm that this ticket is there
        Assert.Null(await host.Service.GetAsync(id, Junior()));
        Assert.Empty(await host.Service.GetMessagesAsync(id, TicketMessageAudience.Intern, Junior()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.GetAsync(id, Citizen()));
    }

    // ---- participants and the internal thread ----

    [Fact]
    public async Task AnAttachedAgentReadsTheTicketAndItsInternalThread()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));
        await host.Service.PostInternalNoteAsync(id, "Interner Vermerk zur Lage.", Leader());

        Assert.Null(await host.Service.GetAsync(id, Junior()));

        await host.Service.AddParticipantAsync(id, "junior", Leader());

        Assert.NotNull(await host.Service.GetAsync(id, Junior()));
        Assert.Single(await host.Service.GetMessagesAsync(id, TicketMessageAudience.Intern, Junior()));
    }

    [Fact]
    public async Task RemovingAnAgentTakesTheInternalThreadWithIt()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));
        await host.Service.PostInternalNoteAsync(id, "Interner Vermerk zur Lage.", Leader());
        await host.Service.AddParticipantAsync(id, "junior", Leader());
        var row = Assert.Single(await host.Service.GetParticipantsAsync(id, Leader()));

        await host.Service.RemoveParticipantAsync(row.Id, Leader());

        Assert.Null(await host.Service.GetAsync(id, Junior()));
        Assert.Empty(await host.Service.GetMessagesAsync(id, TicketMessageAudience.Intern, Junior()));
    }

    [Fact]
    public async Task LeadershipReadsTheInternalThreadWithoutBeingAttached()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));
        await host.Service.PostInternalNoteAsync(id, "Interner Vermerk zur Lage.", Leader());

        Assert.Empty(await host.Service.GetParticipantsAsync(id, Leader()));
        Assert.Single(await host.Service.GetMessagesAsync(id, TicketMessageAudience.Intern, Leader()));
    }

    [Fact]
    public async Task AnUnattachedAgentCannotWriteAnInternalNote()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.PostInternalNoteAsync(id, "Ich schreibe hier einfach mit.", Junior()));
    }

    [Fact]
    public async Task OnlyTheDeskMayAttachAnAgent()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.AddParticipantAsync(id, "junior", Junior()));
    }

    [Fact]
    public async Task AnAttachedAgentMayMarkTheTicketRead()
    {
        // the regression: the detail page calls this in OnParametersSetAsync, and the leadership-only guard threw
        // before the page could render — the whole participation feature was unreachable for its own audience
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));
        await host.Service.AddParticipantAsync(id, "junior", Leader());

        await host.Service.MarkAgentReadAsync(id, Junior());

        await using var db = host.Factory.CreateDbContext();
        // the participant stamps their OWN row; the desk mark is one for the whole house and stays where it was
        Assert.NotNull((await db.TicketBeteiligte.SingleAsync(p => p.AgentId == "junior")).LastReadAt);
        Assert.Null((await db.Tickets.SingleAsync(t => t.Id == id)).AgentLastReadAt);
    }

    [Fact]
    public async Task TheDeskMarkMovesForTheDesk()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await host.Service.MarkAgentReadAsync(id, Leader());

        await using var db = host.Factory.CreateDbContext();
        Assert.NotNull((await db.Tickets.SingleAsync(t => t.Id == id)).AgentLastReadAt);
    }

    [Fact]
    public async Task AnUnattachedAgentMovesNoMark()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await host.Service.MarkAgentReadAsync(id, Junior());

        await using var db = host.Factory.CreateDbContext();
        Assert.Null((await db.Tickets.SingleAsync(t => t.Id == id)).AgentLastReadAt);
    }

    [Fact]
    public async Task ReadingClearsTheParticipationBadge()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());
        var id = await IdAsync(host, caseNumber);
        await host.Service.PostInternalNoteAsync(id, "Antwort der Führung dazu.", Leader());
        Assert.Equal(1, Assert.Single(await host.Service.GetMyParticipationsAsync(Junior())).UnreadInternal);

        await host.Service.MarkAgentReadAsync(id, Junior());

        Assert.Equal(0, Assert.Single(await host.Service.GetMyParticipationsAsync(Junior())).UnreadInternal);
    }

    [Fact]
    public async Task AnInternalTicketHasNoCitizenThreadToAnswerInto()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());
        var id = await IdAsync(host, caseNumber);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ReplyToCitizenAsync(id, "Sehr geehrte Frau Musterfrau,", Leader()));
    }

    [Fact]
    public async Task RestoringAnInternalTicketIgnoresTheCitizenQuota()
    {
        // the citizen quota compared a null column against a null parameter, so it counted every open internal
        // ticket in the house as one account's allowance
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        for (var i = 0; i < TicketRules.MaxOpen; i++)
        {
            await host.Service.OpenAsAgentAsync(
                new TicketInput { Subject = $"Laufendes Anliegen {i}", Text = "Ich brauche eine Entscheidung dazu." },
                [], Junior());
        }
        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Versehentlich gelöscht", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());
        var id = await IdAsync(host, caseNumber);
        await host.Service.DeleteAsync(id, Leader());

        await host.Service.RestoreAsync(id, Leader());

        await using var db = host.Factory.CreateDbContext();
        Assert.False((await db.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == id)).IsDeleted);
    }

    [Fact]
    public async Task TheDeskListNamesTheOpenerOfAnInternalTicket()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());

        var row = Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Leader()));

        Assert.Equal(TicketArt.Intern, row.Kind);
        Assert.Equal("Wren", row.CitizenName);
    }

    // ---- internal tickets ----

    [Fact]
    public async Task AnAgentOpensAnInternalTicketAndIsAttachedToIt()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());

        await using var db = host.Factory.CreateDbContext();
        var row = await db.Tickets.SingleAsync(t => t.CaseNumber == caseNumber);
        Assert.Equal(TicketArt.Intern, row.Kind);
        Assert.Null(row.CitizenProfileId);
        Assert.Equal("junior", row.OpenedByAgentId);
        // the opener is attached, or they would lose their own ticket the moment they leave the page
        Assert.True(await db.TicketBeteiligte.AnyAsync(p => p.TicketId == row.Id && p.AgentId == "junior"));
        // the opening message is the first internal note; an internal ticket has no citizen thread at all
        var message = await db.TicketNachrichten.SingleAsync(m => m.TicketId == row.Id);
        Assert.Equal(TicketMessageAudience.Intern, message.Audience);
    }

    [Fact]
    public async Task AnInternalTicketIgnoresTheCitizenQuotas()
    {
        // the caps count per citizen profile, and an internal ticket has none
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        for (var i = 0; i < TicketRules.PerDay + 2; i++)
        {
            await host.Service.OpenAsAgentAsync(
                new TicketInput { Subject = $"Anliegen {i}", Text = "Ich brauche eine Entscheidung dazu." },
                [], Junior());
        }

        await using var db = host.Factory.CreateDbContext();
        Assert.Equal(TicketRules.PerDay + 2, await db.Tickets.CountAsync(t => t.Kind == TicketArt.Intern));
    }

    [Fact]
    public async Task AnInternalTicketIsOpenedEvenWithTheModuleOff()
    {
        // the switch is the OFF button for the citizen desk, not for the agency's own correspondence
        using var ctx = await SeededAsync(ticketsOn: false);
        var host = NewHost(ctx);

        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());

        Assert.False(string.IsNullOrWhiteSpace(caseNumber));
    }

    [Fact]
    public async Task AnInternalTicketOpensAtTheDesk()
    {
        // no citizen behind it, so the detail projection must survive the LEFT JOIN coming back empty
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());
        var id = await IdAsync(host, caseNumber);

        var detail = await host.Service.GetAsync(id, Junior());

        Assert.NotNull(detail);
        Assert.Equal(TicketArt.Intern, detail!.Kind);
        Assert.Equal("Wren", detail.OpenedByCodename);
        Assert.Equal(string.Empty, detail.CitizenName);
        Assert.False(detail.CitizenIsBlocked);
    }

    [Fact]
    public async Task ACitizenCannotOpenAnInternalTicket()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Citizen()));
    }

    [Fact]
    public async Task TheOwnParticipationListShowsUnreadInternalNotesOfOthersOnly()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await host.Service.OpenAsAgentAsync(
            new TicketInput { Subject = "Frage zur Dienstplanung", Text = "Ich brauche eine Entscheidung dazu." },
            [], Junior());
        var id = await IdAsync(host, caseNumber);
        await host.Service.PostInternalNoteAsync(id, "Antwort der Führung dazu.", Leader());

        var mine = Assert.Single(await host.Service.GetMyParticipationsAsync(Junior()));

        Assert.Equal(caseNumber, mine.CaseNumber);
        // the opener's own line does not count against them
        Assert.Equal(1, mine.UnreadInternal);
    }

    [Fact]
    public async Task The_read_only_supervision_reads_the_desk_but_cannot_answer()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        var rows = await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Supervision());
        Assert.Single(rows);
        Assert.NotNull(await host.Service.GetAsync(id, Supervision()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Service.ReplyToCitizenAsync(id, "Wir prüfen das.", Supervision()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Service.SetStatusAsync(id, TicketStatus.Geschlossen, Supervision()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Service.PostInternalNoteAsync(id, "Notiz", Supervision()));
    }

    [Fact]
    public async Task A_foreign_ticket_is_simply_not_found_for_another_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);

        Assert.Null(await host.Service.GetOwnDetailAsync(caseNumber, Citizen(OtherUserId)));
        Assert.Empty(await host.Service.GetOwnAsync(Citizen(OtherUserId)));
    }

    [Fact]
    public async Task An_account_without_a_civilian_identity_sees_the_page_without_rows()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await OpenAsync(host);

        Assert.Empty(await host.Service.GetOwnAsync(Junior()));
        Assert.Equal(0, await host.Service.GetOwnUnreadCountAsync(Junior()));
    }

    // ---- the conversation ----

    [Fact]
    public async Task An_agency_answer_carries_no_agent_and_waits_for_the_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);

        await host.Service.ReplyToCitizenAsync(id, "Bitte senden Sie uns den Kaufvertrag.", Leader());

        await using var db = ctx.NewContext();
        var answer = await db.TicketNachrichten
            .Where(m => m.Audience == TicketMessageAudience.Buerger && !m.AuthorIsCitizen)
            .SingleAsync();
        Assert.Null(answer.AuthorAgentId);
        var row = await db.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.WartetAufBuerger, row.Status);
        Assert.Equal("lead", row.HandlerId);

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        Assert.Equal(2, detail!.Messages.Count);
        // the outward projection has no author field at all, so there is nothing to strip
        Assert.DoesNotContain("AuthorCodename",
            typeof(CitizenTicketMessage).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task A_citizen_answer_moves_a_waiting_ticket_back_into_handling()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);
        await host.Service.ReplyToCitizenAsync(id, "Bitte senden Sie uns den Kaufvertrag.", Leader());

        await host.Service.ReplyAsCitizenAsync(caseNumber, "Hier ist der Kaufvertrag vom 3. Mai.", Citizen());

        await using var db = ctx.NewContext();
        Assert.Equal(TicketStatus.InBearbeitung, (await db.Tickets.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_citizen_answer_leaves_an_untouched_ticket_open()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);

        await host.Service.ReplyAsCitizenAsync(caseNumber, "Ein Nachtrag zu meinem Anliegen von vorhin.", Citizen());

        await using var db = ctx.NewContext();
        Assert.Equal(TicketStatus.Offen, (await db.Tickets.SingleAsync()).Status);
    }

    [Fact]
    public async Task Closed_is_closed_for_the_citizen_and_leadership_may_reopen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);
        await host.Service.SetStatusAsync(id, TicketStatus.Geschlossen, Leader());

        Assert.False((await host.Service.GetOwnDetailAsync(caseNumber, Citizen()))!.MayReply);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Service.ReplyAsCitizenAsync(caseNumber, "Doch noch eine Frage dazu.", Citizen()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Service.ReplyToCitizenAsync(id, "Nachtrag.", Leader()));

        await host.Service.SetStatusAsync(id, TicketStatus.InBearbeitung, Leader());
        await host.Service.ReplyAsCitizenAsync(caseNumber, "Doch noch eine Frage dazu.", Citizen());

        await using var db = ctx.NewContext();
        var row = await db.Tickets.SingleAsync();
        Assert.Null(row.ClosedAt);
        Assert.Null(row.ClosedById);
    }

    [Fact]
    public async Task An_internal_note_never_reaches_the_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);

        await host.Service.PostInternalNoteAsync(id, "Halter laut Register weiterhin der Bürger.", Leader());

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        Assert.Single(detail!.Messages);
        var internals = await host.Service.GetMessagesAsync(id, TicketMessageAudience.Intern, Leader());
        Assert.Single(internals);
        Assert.Equal("Falcon", internals[0].AuthorCodename);
    }

    [Fact]
    public async Task Only_a_status_that_concerns_the_citizen_rings_their_bell()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        // taking the ticket on is news to the desk, not to the citizen
        await host.Service.AssignSelfAsync(id, Leader());
        await host.Notifications.DidNotReceive().NotifyOnceAsync(Arg.Any<string>(),
            NotificationType.PublicTicketAnswered, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await host.Service.SetStatusAsync(id, TicketStatus.Geschlossen, Leader());
        await host.Notifications.Received(1).NotifyOnceAsync(CitizenUserId,
            NotificationType.PublicTicketAnswered, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unsupported_status_move_is_refused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await host.Service.SetStatusAsync(id, TicketStatus.Geschlossen, Leader());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Service.SetStatusAsync(id, TicketStatus.Offen, Leader()));
    }

    // ---- read marks and counters ----

    [Fact]
    public async Task Each_side_moves_only_its_own_read_mark()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);
        await host.Service.ReplyToCitizenAsync(id, "Wir haben Ihr Anliegen erhalten.", Leader());

        Assert.Equal(1, await host.Service.GetOwnUnreadCountAsync(Citizen()));
        var deskBefore = await host.Service.GetInboxAsync(TicketInboxScope.Wartet, null, false, Leader());
        Assert.Single(deskBefore);
        Assert.Equal(1, deskBefore[0].UnreadCount);

        // the citizen reading clears the citizen count and leaves the desk count alone
        await host.Service.MarkCitizenReadAsync(caseNumber, Citizen());
        Assert.Equal(0, await host.Service.GetOwnUnreadCountAsync(Citizen()));
        var deskAfter = await host.Service.GetInboxAsync(TicketInboxScope.Wartet, null, false, Leader());
        Assert.Equal(1, deskAfter[0].UnreadCount);

        await host.Service.MarkAgentReadAsync(id, Leader());
        var deskRead = await host.Service.GetInboxAsync(TicketInboxScope.Wartet, null, false, Leader());
        Assert.Equal(0, deskRead[0].UnreadCount);
    }

    [Fact]
    public async Task The_supervision_sets_no_read_mark()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await host.Service.MarkAgentReadAsync(id, Supervision());

        await using var db = ctx.NewContext();
        Assert.Null((await db.Tickets.SingleAsync()).AgentLastReadAt);
    }

    [Fact]
    public async Task The_badge_counts_running_tickets_only()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var first = await IdAsync(host, await OpenAsync(host));
        await OpenAsync(host);

        Assert.Equal(2, await host.Service.GetOpenCountAsync());
        await host.Service.SetStatusAsync(first, TicketStatus.Geschlossen, Leader());
        Assert.Equal(1, await host.Service.GetOpenCountAsync());

        Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Leader()));
        Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Geschlossen, null, false, Leader()));
    }

    [Fact]
    public async Task The_desk_marks_a_ticket_awaiting_an_answer_while_the_citizen_spoke_last()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);
        var id = await IdAsync(host, caseNumber);

        var open = await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Leader());
        Assert.True(open[0].AwaitingAnswer);
        Assert.Equal("Erika Musterfrau", open[0].CitizenName);

        await host.Service.ReplyToCitizenAsync(id, "Wir melden uns.", Leader());
        var waiting = await host.Service.GetInboxAsync(TicketInboxScope.Wartet, null, false, Leader());
        Assert.False(waiting[0].AwaitingAnswer);
    }

    [Fact]
    public async Task The_desk_search_finds_by_case_number_subject_and_citizen_name()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await OpenAsync(host);

        Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Offen, caseNumber, false, Leader()));
        Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Offen, "Fahrzeug", false, Leader()));
        Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Offen, "Musterfrau", false, Leader()));
        Assert.Empty(await host.Service.GetInboxAsync(TicketInboxScope.Offen, "Kleber", false, Leader()));
    }

    // ---- trash ----

    [Fact]
    public async Task Deleting_is_a_soft_delete_and_restoring_brings_it_back()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await host.Service.DeleteAsync(id, Leader());
        Assert.Empty(await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Leader()));
        var trash = await host.Service.GetTrashAsync();
        Assert.Single(trash);

        await host.Service.RestoreAsync(id, Leader());
        Assert.Single(await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Leader()));
        Assert.Empty(await host.Service.GetTrashAsync());
    }

    [Fact]
    public async Task The_trash_row_names_the_subject_but_not_the_citizen_or_the_conversation()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));
        await host.Service.DeleteAsync(id, Leader());

        var row = TrashProjection.Ticket((await host.Service.GetTrashAsync()).Single());

        Assert.Equal("tickets", row.Kind);
        Assert.Equal("Frage zu meinem Fahrzeug", row.Title);
        Assert.DoesNotContain("Musterfrau", row.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Kennzeichen", row.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_leadership_may_delete_or_restore()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await IdAsync(host, await OpenAsync(host));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.DeleteAsync(id, Junior()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Service.RestoreAsync(id, Supervision()));
    }

    // ---- the automatic confirmation ----

    private static async Task SeedConfirmationAsync(SqliteTestContext ctx, bool active = true)
    {
        await new PublicTemplateService(ctx.Factory).SaveAsync(
            new PublicTemplateInput(null, PublicTemplateKind.TicketEingang, "Eingang",
                "Guten Tag BUERGER, Ihr Anliegen AKTENZEICHEN ist eingegangen. Mit Gruss NAME", active, 10),
            Leader());
    }

    [Fact]
    public async Task Opening_with_an_active_template_confirms_without_naming_an_agent()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await SeedConfirmationAsync(ctx);

        var caseNumber = await OpenAsync(host);

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        var confirmation = Assert.Single(detail!.Messages, m => !m.FromCitizen);
        Assert.Contains("Erika Musterfrau", confirmation.Text, StringComparison.Ordinal);
        Assert.Contains(caseNumber, confirmation.Text, StringComparison.Ordinal);
        Assert.Contains(PublicTemplateRenderer.Redaction, confirmation.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Falcon", confirmation.Text, StringComparison.Ordinal);

        await using var db = host.Factory.CreateDbContext();
        var row = await db.TicketNachrichten.SingleAsync(m => !m.AuthorIsCitizen);
        Assert.Null(row.AuthorAgentId);
        Assert.Equal(TicketMessageAudience.Buerger, row.Audience);
    }

    [Fact]
    public async Task The_confirmation_moves_no_status_and_rings_no_bell()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await SeedConfirmationAsync(ctx);

        var caseNumber = await OpenAsync(host);

        // an untouched ticket stays Offen; anything else would claim work nobody did
        var ticket = await host.Service.GetAsync(await IdAsync(host, caseNumber), Leader());
        Assert.Equal(TicketStatus.Offen, ticket!.Status);
        // the unread counter shows the confirmation by itself
        Assert.Equal(1, await host.Service.GetOwnUnreadCountAsync(Citizen()));
        await host.Notifications.DidNotReceive().NotifyManyOnceAsync(
            Arg.Any<IReadOnlyList<string>>(), NotificationType.PublicTicketAnswered, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Without_an_active_template_no_confirmation_is_written()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await SeedConfirmationAsync(ctx, active: false);

        var caseNumber = await OpenAsync(host);

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        var only = Assert.Single(detail!.Messages);
        Assert.True(only.FromCitizen);
        // and nothing leaked into the internal thread either
        Assert.Empty(await host.Service.GetMessagesAsync(await IdAsync(host, caseNumber),
            TicketMessageAudience.Intern, Leader()));
    }

    [Fact]
    public async Task A_fresh_ticket_still_awaits_an_answer_although_the_confirmation_is_newer()
    {
        // the audit interceptor stamps one timestamp per SaveChanges, so the opening message and the automatic
        // confirmation are simultaneous — without the tie-break the desk chip would go dark on every new ticket
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await SeedConfirmationAsync(ctx);

        await OpenAsync(host);

        var open = await host.Service.GetInboxAsync(TicketInboxScope.Offen, null, false, Leader());
        Assert.True(open[0].AwaitingAnswer);
        // and the desk's own unread counter ignores the agency line
        Assert.Equal(1, open[0].UnreadCount);
    }
}
