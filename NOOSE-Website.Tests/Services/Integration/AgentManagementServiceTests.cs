using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Services;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Services.Integration;

public class AgentManagementServiceTests
{
    private sealed class Fixture : IDisposable
    {
        public required SqliteTestContext Ctx;
        public required AppDbContext Db;
        public required AgentManagementService Svc;
        public required INotificationService Notifications;
        public required IDiscordWebhookService Discord;
        public void Dispose() { Db.Dispose(); Ctx.Dispose(); }
    }

    private static UserManager<Agent> BuildUserManager(AppDbContext db)
        => new(new UserStore<Agent>(db), Options.Create(new IdentityOptions()),
            new PasswordHasher<Agent>(), Array.Empty<IUserValidator<Agent>>(),
            Array.Empty<IPasswordValidator<Agent>>(), new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), null!, NullLogger<UserManager<Agent>>.Instance);

    private static Fixture Make(string? bootstrapDiscordId = null)
    {
        var ctx = new SqliteTestContext();
        var db = ctx.NewContext();
        var notifications = Substitute.For<INotificationService>();
        var discord = Substitute.For<IDiscordWebhookService>();
        var builder = new ConfigurationBuilder();
        if (bootstrapDiscordId is not null)
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:AdminDiscordId"] = bootstrapDiscordId,
            });
        }
        var svc = new AgentManagementService(BuildUserManager(db), db, ctx.Factory, notifications,
            discord, builder.Build());
        return new Fixture { Ctx = ctx, Db = db, Svc = svc, Notifications = notifications, Discord = discord };
    }

    private static void Persist(SqliteTestContext ctx, params Agent[] agents)
    {
        using var db = ctx.NewContext();
        db.Users.AddRange(agents);
        db.SaveChanges();
    }

    private static Agent NewAgent(string id, AgentStatus status = AgentStatus.Pending,
        Rank? rank = null, Action<Agent>? cfg = null)
        => Seed.Agent(id, rank ?? Rank.JuniorAgent, status, a =>
        {
            a.Rank = rank;
            a.SecurityStamp = System.Guid.NewGuid().ToString();
            a.ConcurrencyStamp = System.Guid.NewGuid().ToString();
            cfg?.Invoke(a);
        });

    private static ClaimsPrincipal Admin(string id = "admin")
        => ClaimsPrincipalBuilder.Agent(id).AsAdmin().WithRank(Rank.Director).WithCodename("Chief").Build();

    private static ClaimsPrincipal Leader(string id = "lead")
        => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SupervisorySpecialAgent).WithCodename("Lead").Build();

    private static ClaimsPrincipal Junior(string id = "jr")
        => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.JuniorAgent).Build();

    private static ClaimsPrincipal OnlyReader(string id = "ro")
        => ClaimsPrincipalBuilder.Agent(id).AsTeamLead().WithRank(Rank.Director).Build();

    private Agent Reload(Fixture f, string id)
    {
        using var db = f.Ctx.NewContext();
        return db.Users.Single(a => a.Id == id);
    }

    // ---- read methods ----

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPending_OrderedByRegistered()
    {
        using var f = Make();
        Persist(f.Ctx,
            NewAgent("p1", AgentStatus.Pending, cfg: a => a.RegisteredAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            NewAgent("p2", AgentStatus.Pending, cfg: a => a.RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            NewAgent("a1", AgentStatus.Active));

        var pending = await f.Svc.GetPendingAsync();

        Assert.Equal(new[] { "p2", "p1" }, pending.Select(a => a.Id));
    }

    [Fact]
    public async Task GetAllAsync_ExcludesApplicants_PendingFirst()
    {
        using var f = Make();
        Persist(f.Ctx,
            NewAgent("act", AgentStatus.Active, cfg: a => a.Codename = "Bravo"),
            NewAgent("pen", AgentStatus.Pending, cfg: a => a.Codename = "Alpha"),
            NewAgent("app", AgentStatus.Applicant));

        var all = await f.Svc.GetAllAsync();

        Assert.DoesNotContain(all, a => a.Id == "app");
        Assert.Equal("pen", all.First().Id);
    }

    [Fact]
    public async Task GetSelectableAsync_OnlyActiveInternalNonTeamLead()
    {
        using var f = Make();
        Persist(f.Ctx,
            NewAgent("ok", AgentStatus.Active),
            NewAgent("tl", AgentStatus.Active, cfg: a => a.IsTeamLead = true),
            // not even with the admin flag on top
            NewAgent("tl-adm", AgentStatus.Active, cfg: a => { a.IsTeamLead = true; a.IsAdmin = true; }),
            NewAgent("partner", AgentStatus.Active, cfg: a => a.PartnerAgency = PartnerAgency.LSPD),
            NewAgent("pen", AgentStatus.Pending));

        var sel = await f.Svc.GetSelectableAsync();

        Assert.Single(sel);
        Assert.Equal("ok", sel[0].Id);
    }

    [Fact]
    public async Task FindAsync_ReturnsAgent_OrNull()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("x"));

        Assert.Equal("x", (await f.Svc.FindAsync("x"))!.Id);
        Assert.Null(await f.Svc.FindAsync("nope"));
    }

    // ---- release / reject ----

    [Fact]
    public async Task ReleaseAsync_ActivatesAndSetsRank_WritesHistoryAndNotifies()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Pending));

        await f.Svc.ReleaseAsync("t", Rank.SpecialAgent, isTRU: true, isHRB: false, Admin());

        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Active, a.Status);
        Assert.Equal(Rank.SpecialAgent, a.Rank);
        Assert.True(a.IsTRU);
        using var db = f.Ctx.NewContext();
        Assert.True(db.AgentRankHistories.Any(h => h.AgentId == "t"));
        await f.Notifications.Received().NotifyAsync("t", NotificationType.Account, Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReleaseAsPartnerAsync_RequiresLeadership()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.ReleaseAsPartnerAsync("t", PartnerAgency.LSPD, PartnerRank.Member, Junior()));
    }

    [Fact]
    public async Task ReleaseAsPartnerAsync_SetsPartnerClearsInternal()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Pending, rank: Rank.Director, cfg: a => a.IsAdmin = true));

        await f.Svc.ReleaseAsPartnerAsync("t", PartnerAgency.LSPD, PartnerRank.Member, Admin());

        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Active, a.Status);
        Assert.Equal(PartnerAgency.LSPD, a.PartnerAgency);
        Assert.Null(a.Rank);
        Assert.False(a.IsAdmin);
    }

    [Fact]
    public async Task RejectAsync_BlocksWithReason()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t"));

        await f.Svc.RejectAsync("t", "  ", Admin());

        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Blocked, a.Status);
        Assert.False(string.IsNullOrWhiteSpace(a.BlockedReason));
    }

    [Fact]
    public async Task PromoteApplicantToAgentAsync_RejectsNonApplicant()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.PromoteApplicantToAgentAsync("t", Rank.SpecialAgent, false, false, Admin()));
    }

    [Fact]
    public async Task PromoteApplicantToAgentAsync_ActivatesApplicant()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Applicant));

        await f.Svc.PromoteApplicantToAgentAsync("t", Rank.SpecialAgent, false, true, Admin());

        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Active, a.Status);
        Assert.True(a.IsHRB);
    }

    // ---- master data / name change ----

    [Fact]
    public async Task MasterDataChangeAsync_EmptyCodenameThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.MasterDataChangeAsync("t", "Real", "  ", "42", Admin()));
    }

    [Fact]
    public async Task MasterDataChangeAsync_OnlyReaderDenied()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.MasterDataChangeAsync("t", "Real", "Cool", "42", OnlyReader()));
    }

    [Fact]
    public async Task MasterDataChangeAsync_UpdatesFields()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.MasterDataChangeAsync("t", "  John  ", "  Ghost  ", "  99  ", Admin());

        var a = Reload(f, "t");
        Assert.Equal("Ghost", a.Codename);
        Assert.Equal("John", a.RealName);
        Assert.Equal("99", a.BadgeNumber);
    }

    [Fact]
    public async Task NameChangeRequest_Then_Approve_Flow()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active, cfg: a => a.Codename = "Old"));

        await f.Svc.NameChangeRequestAsync("t", "New Real", "NewCode", "7", Admin());
        var pendings = await f.Svc.GetPendingNameChangesAsync();
        Assert.Contains(pendings, a => a.Id == "t");

        await f.Svc.NameChangeApproveAsync("t", Admin());
        var a = Reload(f, "t");
        Assert.Equal("NewCode", a.Codename);
        Assert.Null(a.NameChangeRequestedAt);
    }

    [Fact]
    public async Task NameChangeApproveAsync_NoRequestThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Svc.NameChangeApproveAsync("t", Admin()));
    }

    [Fact]
    public async Task NameChangeRejectAsync_ClearsPending()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active,
            cfg: a => { a.PendingCodename = "Want"; a.NameChangeRequestedAt = DateTime.UtcNow; }));

        await f.Svc.NameChangeRejectAsync("t", "no", Admin());

        var a = Reload(f, "t");
        Assert.Null(a.NameChangeRequestedAt);
        Assert.Null(a.PendingCodename);
    }

    // ---- rank / flags ----

    [Fact]
    public async Task RankChangeAsync_RequiresLeadership()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.RankChangeAsync("t", Rank.SeniorSpecialAgent, Junior()));
    }

    [Fact]
    public async Task RankChangeAsync_SetsRank_WritesHistory()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active, rank: Rank.JuniorAgent));

        await f.Svc.RankChangeAsync("t", Rank.SeniorSpecialAgent, Admin());

        Assert.Equal(Rank.SeniorSpecialAgent, Reload(f, "t").Rank);
    }

    [Fact]
    public async Task SetPartnerRankAsync_NonPartnerThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.SetPartnerRankAsync("t", PartnerRank.Special, Admin()));
    }

    [Fact]
    public async Task SetPartnerRankAsync_UpdatesPartner()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active,
            cfg: a => { a.PartnerAgency = PartnerAgency.LSPD; a.PartnerRank = PartnerRank.Member; }));

        await f.Svc.SetPartnerRankAsync("t", PartnerRank.Special, Admin());

        Assert.Equal(PartnerRank.Special, Reload(f, "t").PartnerRank);
    }

    [Fact]
    public async Task ConvertToPartnerAsync_InactiveThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Pending));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.ConvertToPartnerAsync("t", PartnerAgency.LSPD, PartnerRank.Member, Admin()));
    }

    [Fact]
    public async Task ConvertToPartnerAsync_LastAdminBlocked()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("only", AgentStatus.Active, rank: Rank.Director, cfg: a => a.IsAdmin = true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.ConvertToPartnerAsync("only", PartnerAgency.LSPD, PartnerRank.Member, Admin("other")));
    }

    [Fact]
    public async Task ConvertToPartnerAsync_Succeeds()
    {
        using var f = Make();
        Persist(f.Ctx,
            NewAgent("t", AgentStatus.Active, rank: Rank.SpecialAgent),
            NewAgent("keepadmin", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await f.Svc.ConvertToPartnerAsync("t", PartnerAgency.LSMD, PartnerRank.Member, Admin("keepadmin"));

        var a = Reload(f, "t");
        Assert.Equal(PartnerAgency.LSMD, a.PartnerAgency);
        Assert.Null(a.Rank);
    }

    [Fact]
    public async Task ConvertToInternalAsync_AlreadyInternalThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.ConvertToInternalAsync("t", Rank.SpecialAgent, Admin()));
    }

    [Fact]
    public async Task ConvertToInternalAsync_Succeeds()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active,
            cfg: a => { a.PartnerAgency = PartnerAgency.LSPD; a.PartnerRank = PartnerRank.Member; }));

        await f.Svc.ConvertToInternalAsync("t", Rank.SpecialAgent, Admin());

        var a = Reload(f, "t");
        Assert.Null(a.PartnerAgency);
        Assert.Equal(Rank.SpecialAgent, a.Rank);
    }

    // ---- promotion decision ----

    [Fact]
    public async Task PromotionDecideAsync_ApprovesAndSetsRank()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active, rank: Rank.JuniorAgent));
        using (var db = f.Ctx.NewContext())
        {
            db.AgentPromotionRequests.Add(new NOOSE_Website.Data.Entities.Personnel.AgentPromotionRequest
            {
                Id = "req1", AgentId = "t", TargetRank = Rank.SpecialAgent, Status = PromotionStatus.Requested,
            });
            db.SaveChanges();
        }

        await f.Svc.PromotionDecideAsync("req1", approved: true, note: "ok", Admin());

        Assert.Equal(Rank.SpecialAgent, Reload(f, "t").Rank);
    }

    [Fact]
    public async Task PromotionDecideAsync_NotFoundThrows()
    {
        using var f = Make();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.PromotionDecideAsync("nope", true, null, Admin()));
    }

    [Fact]
    public async Task TruSet_HrbSet_ToggleFlags()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.TruSetAsync("t", true, Admin());
        await f.Svc.HrbSetAsync("t", true, Admin());

        var a = Reload(f, "t");
        Assert.True(a.IsTRU);
        Assert.True(a.IsHRB);
    }

    [Fact]
    public async Task TeamLeadSetAsync_OnlyReaderDenied()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.TeamLeadSetAsync("t", true, OnlyReader()));
    }

    [Fact]
    public async Task AdminSetAsync_RequiresAdmin()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.AdminSetAsync("t", true, Leader()));
    }

    [Fact]
    public async Task AdminSetAsync_SelfStripBlocked()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("me", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.AdminSetAsync("me", false, Admin("me")));
    }

    [Fact]
    public async Task AdminSetAsync_LastAdminBlocked()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("only", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.AdminSetAsync("only", false, Admin("other")));
    }

    [Fact]
    public async Task AdminSetAsync_GrantsAdmin()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.AdminSetAsync("t", true, Admin());

        Assert.True(Reload(f, "t").IsAdmin);
    }

    // ---- block / unblock / delete ----

    [Fact]
    public async Task BlockAsync_SelfThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("me", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Svc.BlockAsync("me", "x", Admin("me")));
    }

    [Fact]
    public async Task Block_Then_Unblock()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.BlockAsync("t", "spy", Admin());
        Assert.Equal(AgentStatus.Blocked, Reload(f, "t").Status);

        await f.Svc.UnblockAsync("t", Admin());
        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Active, a.Status);
        Assert.Null(a.BlockedReason);
    }

    [Fact]
    public async Task DeleteAccountAsync_SelfThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("me", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Svc.DeleteAccountAsync("me", Admin("me")));
    }

    [Fact]
    public async Task DeleteAccountAsync_RemovesUser_WritesAudit()
    {
        using var f = Make();
        Persist(f.Ctx,
            NewAgent("t", AgentStatus.Active, cfg: a => a.Codename = "Gone"),
            NewAgent("keepadmin", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await f.Svc.DeleteAccountAsync("t", Admin("keepadmin"));

        using var db = f.Ctx.NewContext();
        Assert.False(db.Users.Any(u => u.Id == "t"));
        Assert.True(db.AuditLogs.Any(l => l.EntityId == "t" && l.Action == AuditAction.Deleted));
    }

    [Fact]
    public async Task DeleteAccountAsync_LastAdminThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("only", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.DeleteAccountAsync("only", Admin("other")));
    }

    // ---- terminate ----

    [Fact]
    public async Task TerminateAsync_SetsStatusAndFields()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active, Rank.SpecialAgent, a => a.Codename = "Falcon"));
        var before = Reload(f, "t").SecurityStamp;

        await f.Svc.TerminateAsync("t", "  Dienstvergehen  ", createNote: false, postDiscord: false, Admin());

        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Terminated, a.Status);
        Assert.NotNull(a.TerminatedAt);
        Assert.Equal("admin", a.TerminatedById);
        Assert.Equal("Chief", a.TerminatedByName);
        Assert.Equal("Dienstvergehen", a.TerminationReason);
        Assert.Equal(Rank.SpecialAgent, a.Rank); // last rank is kept for the roster column
        Assert.NotEqual(before, a.SecurityStamp);
    }

    [Fact]
    public async Task TerminateAsync_DropsAdminRights()
    {
        using var f = Make();
        Persist(f.Ctx,
            NewAgent("t", AgentStatus.Active, cfg: a => a.IsAdmin = true),
            NewAgent("keepadmin", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await f.Svc.TerminateAsync("t", "weg", createNote: false, postDiscord: false, Admin("keepadmin"));

        Assert.False(Reload(f, "t").IsAdmin);
    }

    [Fact]
    public async Task TerminateAsync_CreatesBlacklistEntry()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active, cfg: a =>
        {
            a.Codename = "Falcon";
            a.RealName = "John Doe";
            a.DiscordId = "999";
        }));

        await f.Svc.TerminateAsync("t", "Verrat", createNote: false, postDiscord: false, Admin());

        using var db = f.Ctx.NewContext();
        var sperre = Assert.Single(db.Bewerbungssperren.Where(s => s.AgentId == "t"));
        Assert.True(sperre.IsBlacklist);
        Assert.Null(sperre.BannedUntil);
        Assert.Equal("999", sperre.DiscordId);
        Assert.Equal("John Doe - Falcon", sperre.ApplicantName);
        Assert.Contains("Verrat", sperre.Reason);
    }

    [Fact]
    public async Task TerminateAsync_SupersedesTemporaryBan()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));
        using (var seed = f.Ctx.NewContext())
        {
            seed.Bewerbungssperren.Add(new Bewerbungssperre
            {
                AgentId = "t",
                IsBlacklist = false,
                BannedUntil = DateTime.UtcNow.AddDays(7),
            });
            seed.SaveChanges();
        }

        await f.Svc.TerminateAsync("t", "weg", createNote: false, postDiscord: false, Admin());

        using var db = f.Ctx.NewContext();
        var sperre = Assert.Single(db.Bewerbungssperren.Where(s => s.AgentId == "t"));
        Assert.True(sperre.IsBlacklist);
    }

    [Fact]
    public async Task TerminateAsync_CreateNoteFlagControlsPersonnelEntry()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("with", AgentStatus.Active), NewAgent("without", AgentStatus.Active));

        await f.Svc.TerminateAsync("with", "Grund A", createNote: true, postDiscord: false, Admin());
        await f.Svc.TerminateAsync("without", "Grund B", createNote: false, postDiscord: false, Admin());

        using var db = f.Ctx.NewContext();
        var note = Assert.Single(db.AgentNotes.Where(n => n.AgentId == "with"));
        Assert.Equal(AgentNoteKind.Termination, note.Kind);
        Assert.Contains("Grund A", note.Text);
        Assert.Empty(db.AgentNotes.Where(n => n.AgentId == "without"));
    }

    [Fact]
    public async Task TerminateAsync_NoteEscapesReasonHtml()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.TerminateAsync("t", "<script>alert(1)</script>", createNote: true, postDiscord: false, Admin());

        using var db = f.Ctx.NewContext();
        var note = Assert.Single(db.AgentNotes.Where(n => n.AgentId == "t"));
        Assert.DoesNotContain("<script>", note.Text);
    }

    [Fact]
    public async Task TerminateAsync_PostDiscordFlagControlsWebhook()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.TerminateAsync("t", "weg", createNote: false, postDiscord: false, Admin());

        await f.Discord.DidNotReceiveWithAnyArgs().PushPersonnelEntryAsync(
            default!, default!, default!, default, default!, default!, default);
    }

    [Fact]
    public async Task TerminateAsync_SelfThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("me", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.TerminateAsync("me", "weg", false, false, Admin("me")));
    }

    [Fact]
    public async Task TerminateAsync_EmptyReasonThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.TerminateAsync("t", "   ", false, false, Admin()));
    }

    [Fact]
    public async Task TerminateAsync_AlreadyTerminatedThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Terminated));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.TerminateAsync("t", "weg", false, false, Admin()));
    }

    [Fact]
    public async Task TerminateAsync_ApplicantThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Applicant));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.TerminateAsync("t", "weg", false, false, Admin()));
    }

    [Fact]
    public async Task TerminateAsync_LastAdminThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("only", AgentStatus.Active, cfg: a => a.IsAdmin = true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.TerminateAsync("only", "weg", false, false, Admin("other")));
    }

    [Fact]
    public async Task TerminateAsync_BootstrapAdminThrows()
    {
        using var f = Make(bootstrapDiscordId: "4711");
        Persist(f.Ctx, NewAgent("boot", AgentStatus.Active, cfg: a => a.DiscordId = "4711"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Svc.TerminateAsync("boot", "weg", false, false, Admin()));
    }

    [Fact]
    public async Task TerminateAsync_JuniorDenied()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.TerminateAsync("t", "weg", false, false, Junior()));
    }

    // The read-only barrier interceptor is absent under SQLite, so this proves the explicit guard carries it.
    [Fact]
    public async Task TerminateAsync_OnlyReaderDenied()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.TerminateAsync("t", "weg", false, false, OnlyReader()));
    }

    [Fact]
    public async Task TerminateAsync_WritesAuditLog()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await f.Svc.TerminateAsync("t", "Verrat", false, false, Admin());

        using var db = f.Ctx.NewContext();
        Assert.Contains(db.AuditLogs.Where(l => l.EntityId == "t" && l.Action == AuditAction.Modified),
            l => l.ChangesJson != null && l.ChangesJson.Contains("Gek"));
    }

    [Fact]
    public async Task UnblockAsync_TerminatedThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Terminated));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Svc.UnblockAsync("t", Admin()));
    }

    [Fact]
    public async Task TerminationRevokeAsync_RestoresAgentAndLiftsBlacklist()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active, Rank.SpecialAgent));
        await f.Svc.TerminateAsync("t", "Irrtum", createNote: true, postDiscord: false, Admin());

        await f.Svc.TerminationRevokeAsync("t", Admin());

        var a = Reload(f, "t");
        Assert.Equal(AgentStatus.Active, a.Status);
        Assert.Null(a.TerminatedAt);
        Assert.Null(a.TerminatedById);
        Assert.Null(a.TerminatedByName);
        Assert.Null(a.TerminationReason);
        Assert.Equal(Rank.SpecialAgent, a.Rank);

        using var db = f.Ctx.NewContext();
        Assert.Empty(db.Bewerbungssperren.Where(s => s.AgentId == "t"));
        // the personnel-file entry survives as the historical record
        Assert.Single(db.AgentNotes.Where(n => n.AgentId == "t"));
    }

    [Fact]
    public async Task TerminationRevokeAsync_NotTerminatedThrows()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Active));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Svc.TerminationRevokeAsync("t", Admin()));
    }

    [Fact]
    public async Task TerminationRevokeAsync_OnlyReaderDenied()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("t", AgentStatus.Terminated));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Svc.TerminationRevokeAsync("t", OnlyReader()));
    }

    [Fact]
    public async Task GetSelectableAsync_ExcludesTerminated()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("live", AgentStatus.Active), NewAgent("gone", AgentStatus.Terminated));

        var result = await f.Svc.GetSelectableAsync();

        Assert.Contains(result, a => a.Id == "live");
        Assert.DoesNotContain(result, a => a.Id == "gone");
    }

    [Fact]
    public async Task GetAllAsync_IncludesTerminated()
    {
        using var f = Make();
        Persist(f.Ctx, NewAgent("gone", AgentStatus.Terminated));

        Assert.Contains(await f.Svc.GetAllAsync(), a => a.Id == "gone");
    }
}
