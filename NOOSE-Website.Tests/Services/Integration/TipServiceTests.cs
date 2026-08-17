using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="TipService"/>: who may submit, what a handler sees, and what stays hidden.</summary>
public sealed class TipServiceTests
{
    private const string PersonId = "p1";
    private const string CitizenUserId = "buerger1";
    private const string ProfileId = "profil1";

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal Agent()
        => ClaimsPrincipalBuilder.Agent("agent").WithRank(Rank.SpecialAgent).WithCodename("Wren").Build();

    private static ClaimsPrincipal Citizen(string id = CitizenUserId)
        => ClaimsPrincipalBuilder.Agent(id).WithStatus(AgentStatus.Civilian).Build();

    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("lead", "Falcon", true, false, false);
    }

    private sealed record Host(
        TipService Service,
        PublicWantedService Wanted,
        PublicModuleService Modules,
        ITipAttachmentStorageService Storage,
        INotificationService Notifications,
        IMemoryCache Cache,
        TestDbContextFactory Factory);

    /// <summary>The service with the audit interceptor attached, as in production.</summary>
    /// <remarks>
    /// The interceptor rewrites <c>Remove</c> into a soft delete, which the quota and trash facts depend on.
    /// <see cref="ICaseNumberService"/> is stubbed — the real one issues MySQL-only raw SQL — but it counts up, or the
    /// unique index on the case number would fail on the second submission.
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

        var wanted = new PublicWantedService(factory, modules, caseNumbers,
            Substitute.For<IFileStorageService>(), Substitute.For<IPublicWantedPhotoStorageService>(),
            Substitute.For<INotificationService>(), Substitute.For<IDiscordWebhookService>(), cache);

        var storage = Substitute.For<ITipAttachmentStorageService>();
        storage.IsAllowedType(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0).StartsWith("image/"));
        storage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("gespeichert.jpg");

        var notifications = Substitute.For<INotificationService>();
        var service = new TipService(factory, modules, new BuergerService(factory), wanted, caseNumbers,
            storage, notifications, new TipsBroadcaster());
        return new Host(service, wanted, modules, storage, notifications, cache, factory);
    }

    /// <summary>Seeds module switches, one person file and one complete citizen profile.</summary>
    private static async Task<SqliteTestContext> SeededAsync(
        bool tipsOn = true, Action<BuergerProfil>? profile = null)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        foreach (var key in new[] { PublicModules.Wanted, PublicModules.Tips })
        {
            var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == key);
            row.IsEnabled = key != PublicModules.Tips || tipsOn;
        }

        db.People.Add(Seed.Person(PersonId, "Max Mustermann", p =>
        {
            p.CaseNumber = "NOOSE-P-2026-0001";
            p.WantedReason = "Verdacht auf Waffenhandel";
        }));

        var row2 = new BuergerProfil
        {
            Id = ProfileId,
            UserId = CitizenUserId,
            FirstName = "Erika",
            LastName = "Musterfrau",
        };
        profile?.Invoke(row2);
        db.BuergerProfile.Add(row2);
        await db.SaveChangesAsync();
        return ctx;
    }

    private static Task<string> SubmitAsync(Host host, string? text = null, bool anonymous = false,
        string? reference = null, ClaimsPrincipal? actor = null)
        => host.Service.SubmitAsync(
            new TipInput
            {
                Text = text ?? "Ich habe die gesuchte Person gestern Abend am Hafen gesehen, sie stieg in einen Van.",
                WantsAnonymity = anonymous,
                WantedCaseNumber = reference,
            },
            null, null, null, actor ?? Citizen());

    /// <summary>Publishes a notice for the seeded person and returns its public case number.</summary>
    private static async Task<string> PublishedNoticeAsync(Host host)
    {
        var id = await host.Wanted.CreateDraftFromPersonAsync(PersonId, Leader());
        await host.Wanted.PublishAsync(id, null, Leader());
        await using var db = host.Factory.CreateDbContext();
        return await db.OeffentlicheFahndungen.Where(f => f.Id == id).Select(f => f.CaseNumber!).SingleAsync();
    }

    private static async Task<string> TipIdAsync(Host host, string caseNumber)
    {
        await using var db = host.Factory.CreateDbContext();
        return await db.Hinweise.Where(h => h.CaseNumber == caseNumber).Select(h => h.Id).SingleAsync();
    }

    // ---- submitting ----

    [Fact]
    public async Task Submitting_mints_a_case_number_and_stores_the_text()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        var caseNumber = await SubmitAsync(host);

        Assert.StartsWith("NOOSE-H-", caseNumber, StringComparison.Ordinal);
        await using var db = ctx.NewContext();
        var row = await db.Hinweise.SingleAsync();
        Assert.Equal(ProfileId, row.CitizenProfileId);
        Assert.Equal(TipStatus.Neu, row.Status);
        Assert.Null(row.WantedId);
    }

    [Fact]
    public async Task A_tip_below_the_minimum_length_is_refused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host, "Da war wer."));
    }

    [Fact]
    public async Task The_module_switch_blocks_a_submission_even_when_the_page_is_reachable()
    {
        using var ctx = await SeededAsync(tipsOn: false);
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host));
    }

    [Fact]
    public async Task The_module_switch_does_not_strand_a_running_conversation()
    {
        // same call as with the citizen registration: the switch stops new tips, it does not lock people out of a
        // conversation the agency itself started
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host);
        var id = await TipIdAsync(host, caseNumber);
        await host.Service.AskCitizenAsync(id, "Können Sie das Kennzeichen nennen?", Agent());

        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Tips);
            row.IsEnabled = false;
            await db.SaveChangesAsync();
        }
        host.Cache.Remove("OeffentlicheModule");

        await host.Service.ReplyAsCitizenAsync(caseNumber, "Es war ein weißer Van, HB 42 XY.", Citizen());
        Assert.Equal(2, (await host.Service.GetOwnDetailAsync(caseNumber, Citizen()))!.Messages.Count);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host));
    }

    [Fact]
    public async Task A_blocked_account_may_not_submit()
    {
        using var ctx = await SeededAsync(profile: p =>
        {
            p.IsBlocked = true;
            p.BlockedReason = "Falschmeldungen";
        });
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SubmitAsync(host));
    }

    [Fact]
    public async Task An_incomplete_profile_may_not_submit()
    {
        using var ctx = await SeededAsync(profile: p => p.LastName = string.Empty);
        var host = NewHost(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host));
    }

    // ---- the quota ----

    [Fact]
    public async Task The_daily_quota_blocks_the_next_tip()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);

        for (var i = 0; i < TipRules.PerDay; i++)
        {
            await SubmitAsync(host);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host));
    }

    [Fact]
    public async Task A_deleted_tip_does_not_refill_the_quota()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        for (var i = 0; i < TipRules.PerDay; i++)
        {
            await SubmitAsync(host);
        }

        // the whole point of counting with IgnoreQueryFilters: deleting must not buy another slot
        await using (var db = ctx.NewContext())
        {
            var ids = await db.Hinweise.Select(h => h.Id).ToListAsync();
            foreach (var id in ids)
            {
                await host.Service.DeleteAsync(id, Leader());
            }
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host));
    }

    // ---- the reference ----

    [Fact]
    public async Task A_reference_to_a_published_notice_is_kept()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var notice = await PublishedNoticeAsync(host);

        await SubmitAsync(host, reference: notice);

        await using var db = ctx.NewContext();
        var row = await db.Hinweise.SingleAsync();
        Assert.NotNull(row.WantedId);
    }

    [Fact]
    public async Task A_reference_to_a_notice_that_is_not_published_is_refused()
    {
        // an unpublished draft must not be confirmable from outside; accepting it would make the form an oracle
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await host.Wanted.CreateDraftFromPersonAsync(PersonId, Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SubmitAsync(host, reference: "NOOSE-FA-2026-0001"));
    }

    // ---- anonymity ----

    [Fact]
    public async Task An_anonymous_tip_shows_the_handler_no_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host, anonymous: true);
        var id = await TipIdAsync(host, caseNumber);

        var detail = await host.Service.GetAsync(id, Agent());

        Assert.NotNull(detail);
        Assert.True(detail!.WantsAnonymity);
        Assert.Null(detail.CitizenName);
        Assert.Null(detail.CitizenConfirmedTips);

        var rows = await host.Service.GetInboxAsync(TipInboxScope.Eingang, null, false, Agent());
        Assert.True(rows.Single().IsAnonymous);
        Assert.Null(rows.Single().CitizenName);
    }

    [Fact]
    public async Task A_named_tip_shows_the_handler_the_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await TipIdAsync(host, await SubmitAsync(host));

        var detail = await host.Service.GetAsync(id, Agent());

        Assert.Equal("Erika Musterfrau", detail!.CitizenName);
    }

    [Fact]
    public async Task Resolving_anonymity_is_leadership_only()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await TipIdAsync(host, await SubmitAsync(host, anonymous: true));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.ResolveAnonymityAsync(id, "Belohnung", Agent()));
    }

    [Fact]
    public async Task Resolving_anonymity_needs_a_reason_and_writes_an_audit_row()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await TipIdAsync(host, await SubmitAsync(host, anonymous: true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ResolveAnonymityAsync(id, "  ", Leader()));

        await host.Service.ResolveAnonymityAsync(id, "Auszahlung einer Belohnung", Leader());

        var detail = await host.Service.GetAsync(id, Agent());
        Assert.True(detail!.AnonymityResolved);
        Assert.Equal("Erika Musterfrau", detail.CitizenName);

        await using var db = ctx.NewContext();
        var log = await db.AuditLogs.SingleAsync(a => a.EntityType == "Hinweis" && a.EntityId == id
            && a.ChangesJson != null && a.ChangesJson.Contains("Begr"));
        Assert.Contains("Auszahlung einer Belohnung", log.ChangesJson!, StringComparison.Ordinal);
    }

    // ---- the conversation ----

    [Fact]
    public async Task The_citizen_view_of_an_agency_message_carries_no_author()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host);
        var id = await TipIdAsync(host, caseNumber);

        await host.Service.AskCitizenAsync(id, "Können Sie das Kennzeichen nennen?", Agent());

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        var message = Assert.Single(detail!.Messages);
        Assert.False(message.FromCitizen);
        // structural, not filtered: the record has no author property at all
        Assert.DoesNotContain(typeof(NOOSE_Website.Models.Public.CitizenTipMessage).GetProperties(),
            p => p.Name.Contains("Author", StringComparison.Ordinal));

        await using var db = ctx.NewContext();
        Assert.Null((await db.HinweisNachrichten.SingleAsync()).AuthorAgentId);
    }

    [Fact]
    public async Task An_internal_note_never_reaches_the_citizen()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host);
        var id = await TipIdAsync(host, caseNumber);

        await host.Service.PostInternalNoteAsync(id, "Abgleich mit der Akte läuft.", Agent());

        var detail = await host.Service.GetOwnDetailAsync(caseNumber, Citizen());
        Assert.Empty(detail!.Messages);
        Assert.Single(await host.Service.GetMessagesAsync(id, TipMessageAudience.Intern, Agent()));
    }

    [Fact]
    public async Task Only_the_owner_may_reply_to_a_tip()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ReplyAsCitizenAsync(caseNumber, "Ich war das auch", Citizen("fremder")));
    }

    [Fact]
    public async Task A_citizen_may_not_open_the_inbox()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        await SubmitAsync(host);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => host.Service.GetInboxAsync(TipInboxScope.Eingang, null, false, Citizen()));
    }

    [Fact]
    public async Task The_unread_count_moves_with_the_read_mark()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host);
        var id = await TipIdAsync(host, caseNumber);
        await host.Service.AskCitizenAsync(id, "Bitte um das Kennzeichen.", Agent());

        Assert.Equal(1, await host.Service.GetOwnUnreadCountAsync(Citizen()));
        await host.Service.MarkCitizenReadAsync(caseNumber, Citizen());
        Assert.Equal(0, await host.Service.GetOwnUnreadCountAsync(Citizen()));
    }

    // ---- status ----

    [Fact]
    public async Task The_status_whitelist_refuses_an_undefined_move()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await TipIdAsync(host, await SubmitAsync(host));

        // Neu straight to "führte zur Ergreifung" skips every check the phase exists for
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.SetStatusAsync(id, TipStatus.FuehrteZurErgreifung, Agent()));

        await host.Service.SetStatusAsync(id, TipStatus.InPruefung, Agent());
        await host.Service.SetStatusAsync(id, TipStatus.FuehrteZurErgreifung, Agent());
    }

    [Fact]
    public async Task A_closed_tip_takes_no_further_message()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var caseNumber = await SubmitAsync(host);
        var id = await TipIdAsync(host, caseNumber);
        await host.Service.SetStatusAsync(id, TipStatus.InPruefung, Agent());
        await host.Service.SetStatusAsync(id, TipStatus.Verworfen, Agent());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.ReplyAsCitizenAsync(caseNumber, "Doch noch etwas gesehen", Citizen()));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Service.AskCitizenAsync(id, "Nachfrage", Agent()));
    }

    // ---- the attachment ----

    [Fact]
    public async Task The_attachment_is_readable_by_the_owner_and_a_handler_only()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("bild"));
        var caseNumber = await host.Service.SubmitAsync(
            new TipInput { Text = new string('x', TipRules.MinLength + 1) },
            content, "image/jpeg", "handy.jpg", Citizen());
        var id = await TipIdAsync(host, caseNumber);

        Assert.NotNull(await host.Service.GetAttachmentAsync(id, Citizen()));
        Assert.NotNull(await host.Service.GetAttachmentAsync(id, Agent()));
        Assert.Null(await host.Service.GetAttachmentAsync(id, Citizen("fremder")));
    }

    [Fact]
    public async Task A_non_image_attachment_is_refused()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.SubmitAsync(
            new TipInput { Text = new string('x', TipRules.MinLength + 1) },
            content, "application/pdf", "beweis.pdf", Citizen()));
    }

    [Fact]
    public void Attachments_are_stored_outside_wwwroot()
    {
        var path = new FileUploadOptions().TipsPath.Replace('\\', '/');

        Assert.StartsWith("App_Data/", path, StringComparison.Ordinal);
        Assert.DoesNotContain("wwwroot", path, StringComparison.OrdinalIgnoreCase);
    }

    // ---- trash ----

    [Fact]
    public async Task Deleting_is_a_soft_delete_and_restoring_brings_the_tip_back()
    {
        using var ctx = await SeededAsync();
        var host = NewHost(ctx);
        var id = await TipIdAsync(host, await SubmitAsync(host));

        await host.Service.DeleteAsync(id, Agent());
        Assert.Single(await host.Service.GetTrashAsync());
        Assert.Empty(await host.Service.GetInboxAsync(TipInboxScope.Eingang, null, false, Agent()));

        await host.Service.RestoreAsync(id, Agent());
        Assert.Empty(await host.Service.GetTrashAsync());
        Assert.Single(await host.Service.GetInboxAsync(TipInboxScope.Eingang, null, false, Agent()));
    }
}
