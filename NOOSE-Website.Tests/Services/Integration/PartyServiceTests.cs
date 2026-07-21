using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Parties;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="PartyService"/> against in-memory SQLite.</summary>
public sealed class PartyServiceTests
{
    // Rank >= SupervisorySpecialAgent(4) or admin => leadership.
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    // Junior agent: not leadership, not admin, cannot read classified.
    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.JuniorAgent).Build();

    private static ViewerScope LeaderScope() => ViewerScope.From(Leader());
    private static ViewerScope PlainScope() => ViewerScope.From(Junior());

    private static PartyService NewService(SqliteTestContext ctx,
        IPersonService? person = null, IThreatScoreService? threat = null, IProfileSuggestionService? suggestion = null)
    {
        var caseNo = Substitute.For<ICaseNumberService>();
        caseNo.NextAsync(Arg.Any<AppDbContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("NOOSE-PT-2026-0001");
        return new PartyService(
            ctx.Factory,
            caseNo,
            suggestion ?? Substitute.For<IProfileSuggestionService>(),
            person ?? Substitute.For<IPersonService>(),
            threat ?? Substitute.For<IThreatScoreService>());
    }

    private static Party NewParty(string id, string name = "Grove Street", Action<Party>? configure = null)
    {
        var p = new Party
        {
            Id = id,
            Name = name,
            CaseNumber = $"NOOSE-PT-2026-{id}",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        configure?.Invoke(p);
        return p;
    }

    // ---- GetListAsync ----

    [Fact]
    public async Task GetListAsync_ReturnsNonDeleted_OrderedByModifiedDescending()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("a", "Alpha", p => p.ModifiedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.Parties.Add(NewParty("b", "Bravo", p => p.ModifiedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetListAsync(LeaderScope());

        Assert.Equal(new[] { "b", "a" }, result.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task GetListAsync_ExcludesClassified_ForPlainScope()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("open", "Open"));
            db.Parties.Add(NewParty("secret", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var forPlain = await svc.GetListAsync(PlainScope());
        var forLeader = await svc.GetListAsync(LeaderScope());

        Assert.Equal(new[] { "open" }, forPlain.Select(p => p.Id).ToArray());
        Assert.Equal(2, forLeader.Count);
    }

    // ---- GetDetailAsync ----

    [Fact]
    public async Task GetDetailAsync_ReturnsParty_WhenVisible()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Ballas"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetDetailAsync("p1", LeaderScope());

        Assert.NotNull(result);
        Assert.Equal("Ballas", result!.Name);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsNull_WhenClassified_AndPlainScope()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Ballas", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        Assert.Null(await svc.GetDetailAsync("p1", PlainScope()));
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsNull_WhenNotFound()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        Assert.Null(await svc.GetDetailAsync("missing", LeaderScope()));
    }

    // ---- GetTrashAsync ----

    [Fact]
    public async Task GetTrashAsync_ReturnsOnlyDeleted()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("live", "Live"));
            db.Parties.Add(NewParty("dead", "Dead", p =>
            {
                p.IsDeleted = true;
                p.DeletedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            }));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetTrashAsync();

        Assert.Equal(new[] { "dead" }, result.Select(p => p.Id).ToArray());
    }

    // ---- SearchAsync ----

    [Fact]
    public async Task SearchAsync_FiltersByNameOrCaseNumber()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("a", "Grove Street Families"));
            db.Parties.Add(NewParty("b", "Ballas"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var byName = await svc.SearchAsync("Grove", isLeadership: true);
        var byCase = await svc.SearchAsync("NOOSE-PT-2026-b", isLeadership: true);

        Assert.Equal(new[] { "a" }, byName.Select(p => p.Id).ToArray());
        Assert.Equal(new[] { "b" }, byCase.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task SearchAsync_ExcludesClassified_ForNonLeadership()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("open", "Open"));
            db.Parties.Add(NewParty("secret", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var nonLead = await svc.SearchAsync(null, isLeadership: false);
        var lead = await svc.SearchAsync(null, isLeadership: true);

        Assert.Equal(new[] { "open" }, nonLead.Select(p => p.Id).ToArray());
        Assert.Equal(2, lead.Count);
    }

    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_PersistsParty_WithMemberAndCreatorAgent()
    {
        using var ctx = new SqliteTestContext();
        var person = Seed.Person("person-1", "Big Smoke");
        using (var db = ctx.NewContext())
        {
            db.People.Add(person);
            db.SaveChanges();
        }
        var svc = NewService(ctx);
        var input = new PartyInput
        {
            Name = "  Grove Street  ",
            Members = { new PartyMemberInput { PersonId = "person-1", Role = "Boss", IsLead = true } },
        };

        var created = await svc.CreateAsync(input, Leader());

        Assert.Equal("NOOSE-PT-2026-0001", created.CaseNumber);
        Assert.Equal("Grove Street", created.Name);

        using var check = ctx.NewContext();
        var members = await check.PartyMembers.Where(m => m.PartyId == created.Id).ToListAsync();
        Assert.Single(members);
        Assert.Equal("person-1", members[0].PersonId);
        Assert.True(members[0].IsLead);

        var agents = await check.PartyAgents.Where(a => a.PartyId == created.Id).ToListAsync();
        Assert.Single(agents);
        Assert.Equal("lead", agents[0].AgentId);
        Assert.True(agents[0].IsInvestigationLead);
    }

    [Fact]
    public async Task CreateAsync_AddsClassificationHistory_WhenClassified()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);
        var input = new PartyInput
        {
            Name = "Verdachtsfall",
            Classification = Classification.ReviewCase,
            ClassificationJustification = "Verdacht auf Waffenhandel",
        };

        var created = await svc.CreateAsync(input, Leader());

        using var check = ctx.NewContext();
        var stored = await check.Parties.SingleAsync(p => p.Id == created.Id);
        Assert.Equal(Classification.ReviewCase, stored.Classification);
        var history = await check.ClassificationHistory
            .Where(e => e.EntityType == nameof(Party) && e.EntityId == created.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal(Classification.ReviewCase, history[0].Value);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenMayNotAssignSecrecyLevel()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);
        var input = new PartyInput { Name = "Secret", SecrecyLevel = DocumentClassification.Leadership };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CreateAsync(input, Junior()));
    }

    // ---- RefreshAsync ----

    [Fact]
    public async Task RefreshAsync_UpdatesFields()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Old Name"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);
        var input = new PartyInput { Name = "  New Name  ", Description = "Desc", Targets = "Targets" };

        await svc.RefreshAsync("p1", input, Leader());

        using var check = ctx.NewContext();
        var stored = await check.Parties.SingleAsync(p => p.Id == "p1");
        Assert.Equal("New Name", stored.Name);
        Assert.Equal("Desc", stored.Description);
        Assert.Equal("Targets", stored.Targets);
    }

    [Fact]
    public async Task RefreshAsync_Throws_OnUnknownId()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefreshAsync("missing", new PartyInput { Name = "X" }, Leader()));
    }

    [Fact]
    public async Task RefreshAsync_Throws_WhenClassified_AndNotPrivileged()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.RefreshAsync("p1", new PartyInput { Name = "X" }, Junior()));
    }

    // ---- DeleteAsync ----

    [Fact]
    public async Task DeleteAsync_RemovesParty()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.DeleteAsync("p1", Leader());

        // interceptor absent in tests => hard delete; row gone from the filtered set.
        using var check = ctx.NewContext();
        Assert.False(await check.Parties.AnyAsync(p => p.Id == "p1"));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenNotLeadership()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteAsync("p1", Junior()));
    }

    // ---- RestoreAsync ----

    [Fact]
    public async Task RestoreAsync_UndeletesParty()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Dead", p =>
            {
                p.IsDeleted = true;
                p.DeletedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
                p.DeletedById = "someone";
            }));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.RestoreAsync("p1", Leader());

        using var check = ctx.NewContext();
        var stored = await check.Parties.SingleAsync(p => p.Id == "p1");
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
        Assert.Null(stored.DeletedById);
    }

    [Fact]
    public async Task RestoreAsync_Throws_WhenNotLeadership()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RestoreAsync("p1", Junior()));
    }

    // ---- ClassificationSetAsync ----

    [Fact]
    public async Task ClassificationSetAsync_SetsValue_AndRecordsHistory()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.ClassificationSetAsync("p1", Classification.SuspicionCase, "Grund", Leader());

        using var check = ctx.NewContext();
        var stored = await check.Parties.SingleAsync(p => p.Id == "p1");
        Assert.Equal(Classification.SuspicionCase, stored.Classification);
        var history = await check.ClassificationHistory
            .Where(e => e.EntityType == nameof(Party) && e.EntityId == "p1").ToListAsync();
        Assert.Single(history);
        Assert.Equal("Grund", history[0].Justification);
    }

    [Fact]
    public async Task ClassificationSetAsync_Throws_OnHighestWithoutRank()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        // rank gate: "SecuredStateThreatening" requires Senior Special Agent+ or admin.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ClassificationSetAsync("p1", Classification.SecuredStateThreatening, null, Junior()));
    }

    [Fact]
    public async Task ClassificationSetAsync_Throws_WhenClassified_AndNotPrivileged()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.ClassificationSetAsync("p1", Classification.ReviewCase, null, Junior()));
    }

    // ---- GetClassificationHistoryAsync ----

    [Fact]
    public async Task GetClassificationHistoryAsync_ReturnsEntries_OrderedByTimestampDescending()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.ClassificationHistory.Add(new ClassificationHistory
            {
                EntityType = nameof(Party), EntityId = "p1", Value = Classification.ReviewCase,
                Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.ClassificationHistory.Add(new ClassificationHistory
            {
                EntityType = nameof(Party), EntityId = "p1", Value = Classification.SuspicionCase,
                Timestamp = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetClassificationHistoryAsync("p1", LeaderScope());

        Assert.Equal(2, result.Count);
        Assert.Equal(Classification.SuspicionCase, result[0].Value);
    }

    [Fact]
    public async Task GetClassificationHistoryAsync_ReturnsEmpty_WhenNotVisible()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.ClassificationHistory.Add(new ClassificationHistory
            {
                EntityType = nameof(Party), EntityId = "p1", Value = Classification.ReviewCase,
                Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetClassificationHistoryAsync("p1", PlainScope());

        Assert.Empty(result);
    }

    // ---- GetMembersAsync ----

    [Fact]
    public async Task GetMembersAsync_ReturnsVisibleMembers_LeadsFirst()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.People.Add(Seed.Person("m1", "Zulu Member"));
            db.People.Add(Seed.Person("m2", "Alpha Member"));
            db.PartyMembers.Add(new PartyMember { PartyId = "p1", PersonId = "m1", IsLead = false });
            db.PartyMembers.Add(new PartyMember { PartyId = "p1", PersonId = "m2", IsLead = true });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetMembersAsync("p1", LeaderScope());

        Assert.Equal(new[] { "m2", "m1" }, result.Select(m => m.PersonId).ToArray());
    }

    // ---- MemberAddAsync ----

    [Fact]
    public async Task MemberAddAsync_AddsMember_AndRecalculatesScore()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.People.Add(Seed.Person("person-1", "Ryder"));
            db.SaveChanges();
        }
        var threat = Substitute.For<IThreatScoreService>();
        var svc = NewService(ctx, threat: threat);

        await svc.MemberAddAsync("p1", new PartyMemberInput { PersonId = "person-1", Role = "Soldier" }, Leader());

        using var check = ctx.NewContext();
        var member = await check.PartyMembers.SingleAsync(m => m.PartyId == "p1" && m.PersonId == "person-1");
        Assert.Equal("Soldier", member.Role);
        await threat.Received(1).NewCalculatePersonScoreAsync("person-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemberAddAsync_Throws_OnDuplicateMember()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.People.Add(Seed.Person("person-1"));
            db.PartyMembers.Add(new PartyMember { PartyId = "p1", PersonId = "person-1" });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.MemberAddAsync("p1", new PartyMemberInput { PersonId = "person-1" }, Leader()));
    }

    [Fact]
    public async Task MemberAddAsync_Throws_WhenClassified_AndNotPrivileged()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.People.Add(Seed.Person("person-1"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.MemberAddAsync("p1", new PartyMemberInput { PersonId = "person-1" }, Junior()));
    }

    // ---- MemberChangeAsync ----

    [Fact]
    public async Task MemberChangeAsync_UpdatesRoleAndLead()
    {
        using var ctx = new SqliteTestContext();
        var member = new PartyMember { PartyId = "p1", PersonId = "person-1", Role = "Old", IsLead = false };
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.People.Add(Seed.Person("person-1"));
            db.PartyMembers.Add(member);
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.MemberChangeAsync(member.Id, "  New Role  ", isLead: true, Leader());

        using var check = ctx.NewContext();
        var stored = await check.PartyMembers.SingleAsync(m => m.Id == member.Id);
        Assert.Equal("New Role", stored.Role);
        Assert.True(stored.IsLead);
    }

    [Fact]
    public async Task MemberChangeAsync_Throws_OnUnknownMember()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.MemberChangeAsync("missing", "Role", false, Leader()));
    }

    // ---- MemberRemoveAsync ----

    [Fact]
    public async Task MemberRemoveAsync_RemovesMember()
    {
        using var ctx = new SqliteTestContext();
        var member = new PartyMember { PartyId = "p1", PersonId = "person-1" };
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.People.Add(Seed.Person("person-1"));
            db.PartyMembers.Add(member);
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.MemberRemoveAsync(member.Id, Leader());

        // interceptor absent => hard delete; row gone from the filtered set.
        using var check = ctx.NewContext();
        Assert.False(await check.PartyMembers.AnyAsync(m => m.Id == member.Id));
    }

    [Fact]
    public async Task MemberRemoveAsync_NoOp_WhenNotFound()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        // returns without throwing when the membership does not exist.
        await svc.MemberRemoveAsync("missing", Leader());

        using var check = ctx.NewContext();
        Assert.Equal(0, await check.PartyMembers.CountAsync());
    }

    // ---- GetAgentsAsync ----

    [Fact]
    public async Task GetAgentsAsync_ReturnsAllocations_LeadsFirstThenByCodename()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.Users.Add(Seed.Agent("ag-lead", configure: a => a.Codename = "Zeta"));
            db.Users.Add(Seed.Agent("ag-plain", configure: a => a.Codename = "Alpha"));
            db.PartyAgents.Add(new PartyAgent { PartyId = "p1", AgentId = "ag-plain", IsInvestigationLead = false });
            db.PartyAgents.Add(new PartyAgent { PartyId = "p1", AgentId = "ag-lead", IsInvestigationLead = true });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetAgentsAsync("p1");

        Assert.Equal(new[] { "ag-lead", "ag-plain" }, result.Select(a => a.AgentId).ToArray());
    }

    // ---- GetInvestigationLeadAsync ----

    [Fact]
    public async Task GetInvestigationLeadAsync_ReturnsOnlyLeads()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.Users.Add(Seed.Agent("ag-lead", configure: a => a.Codename = "Lead"));
            db.Users.Add(Seed.Agent("ag-plain", configure: a => a.Codename = "Plain"));
            db.PartyAgents.Add(new PartyAgent { PartyId = "p1", AgentId = "ag-lead", IsInvestigationLead = true });
            db.PartyAgents.Add(new PartyAgent { PartyId = "p1", AgentId = "ag-plain", IsInvestigationLead = false });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetInvestigationLeadAsync("p1");

        Assert.Single(result);
        Assert.Equal("ag-lead", result[0].AgentId);
    }

    // ---- AgentAllocateAsync ----

    [Fact]
    public async Task AgentAllocateAsync_AddsAllocation()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.Users.Add(Seed.Agent("target"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.AgentAllocateAsync("p1", "target", asInvestigationLead: false, Leader());

        using var check = ctx.NewContext();
        var alloc = await check.PartyAgents.SingleAsync(a => a.PartyId == "p1" && a.AgentId == "target");
        Assert.False(alloc.IsInvestigationLead);
    }

    [Fact]
    public async Task AgentAllocateAsync_Throws_WhenNotLeadershipOrEL()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.Users.Add(Seed.Agent("target"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        // junior is neither leadership nor an investigation lead of this file.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.AgentAllocateAsync("p1", "target", asInvestigationLead: false, Junior()));
    }

    // ---- AgentRemoveAsync ----

    [Fact]
    public async Task AgentRemoveAsync_RemovesAllocation()
    {
        using var ctx = new SqliteTestContext();
        var alloc = new PartyAgent { PartyId = "p1", AgentId = "target" };
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.PartyAgents.Add(alloc);
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.AgentRemoveAsync(alloc.Id, Leader());

        using var check = ctx.NewContext();
        Assert.False(await check.PartyAgents.AnyAsync(a => a.Id == alloc.Id));
    }

    [Fact]
    public async Task AgentRemoveAsync_NoOp_WhenNotFound()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await svc.AgentRemoveAsync("missing", Leader());

        using var check = ctx.NewContext();
        Assert.Equal(0, await check.PartyAgents.CountAsync());
    }

    // ---- InvestigationLeadSetAsync ----

    [Fact]
    public async Task InvestigationLeadSetAsync_SetsFlag()
    {
        using var ctx = new SqliteTestContext();
        var alloc = new PartyAgent { PartyId = "p1", AgentId = "target", IsInvestigationLead = false };
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.PartyAgents.Add(alloc);
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.InvestigationLeadSetAsync(alloc.Id, @is: true, Leader());

        using var check = ctx.NewContext();
        var stored = await check.PartyAgents.SingleAsync(a => a.Id == alloc.Id);
        Assert.True(stored.IsInvestigationLead);
    }

    [Fact]
    public async Task InvestigationLeadSetAsync_Throws_WhenNotLeadership()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.InvestigationLeadSetAsync("any-id", true, Junior()));
    }

    // ---- GetHistoryAsync ----

    [Fact]
    public async Task GetHistoryAsync_ReturnsAuditForPartyAndMembers()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1"));
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Party), EntityId = "p1", Action = AuditAction.Created,
                Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "SomethingElse", EntityId = "p1", Action = AuditAction.Modified,
                Timestamp = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetHistoryAsync("p1", isLeadership: true);

        Assert.Single(result);
        Assert.Equal(nameof(Party), result[0].EntityType);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmpty_WhenNotVisible()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Parties.Add(NewParty("p1", "Secret", p => p.SecrecyLevel = DocumentClassification.Leadership));
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Party), EntityId = "p1", Action = AuditAction.Created,
                Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var result = await svc.GetHistoryAsync("p1", isLeadership: false);

        Assert.Empty(result);
    }
}
