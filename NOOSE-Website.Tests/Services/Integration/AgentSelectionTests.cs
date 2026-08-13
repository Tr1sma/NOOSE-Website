using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="AgentSelection"/> over in-memory SQLite.</summary>
/// <remarks>
/// One fixed matrix, every predicate asserted by exact membership. Every agent option list in the app
/// routes through here, so a silent widening of any rule has to fail loudly in this file.
/// </remarks>
public sealed class AgentSelectionTests
{
    /// <summary>Every account shape the roster can hold, with the expected verdict of each predicate.</summary>
    private static readonly Row[] Matrix =
    [
        new("active", AgentStatus.Active, null, true, true, true, true),
        // read-only supervision is RP-wide invisible — except in the audit viewer, on purpose
        new("teamlead", AgentStatus.Active, a => a.IsTeamLead = true, false, false, true, false),
        // not even with the admin flag on top: the marker itself hides the account
        new("teamlead-admin", AgentStatus.Active, a => { a.IsTeamLead = true; a.IsAdmin = true; }, false, false, true, false),
        new("partner", AgentStatus.Active, a => a.PartnerAgency = PartnerAgency.LSPD, false, false, true, true),
        new("partner-teamlead", AgentStatus.Active, a => { a.PartnerAgency = PartnerAgency.DoJ; a.IsTeamLead = true; }, false, false, true, false),
        // blank codename = never released, so there is no agent to name
        new("pending-blank", AgentStatus.Pending, a => a.Codename = string.Empty, false, false, false, true),
        new("pending-named", AgentStatus.Pending, null, false, true, true, true),
        new("blocked", AgentStatus.Blocked, null, false, true, true, false),
        new("terminated", AgentStatus.Terminated, null, false, true, true, true),
        new("applicant", AgentStatus.Applicant, a => a.Codename = string.Empty, false, false, false, false),
        // a citizen of the public area is no agent at all: no picker, no filter, no roster, no personnel file
        new("civilian", AgentStatus.Civilian, a => a.Codename = string.Empty, false, false, false, false),
    ];

    private sealed record Row(
        string Id, AgentStatus Status, Action<Agent>? Configure,
        bool Selectable, bool Listable, bool AuditFilterable, bool PersonnelFile);

    private static async Task<SqliteTestContext> SeededAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        foreach (var row in Matrix)
        {
            db.Users.Add(Seed.Agent(row.Id, status: row.Status, configure: row.Configure));
        }
        await db.SaveChangesAsync();
        return ctx;
    }

    private static string[] Expected(Func<Row, bool> pick)
        => Matrix.Where(pick).Select(r => r.Id).OrderBy(id => id).ToArray();

    [Fact]
    public async Task OnlySelectable_ReturnsExactlyTheExpectedAgents()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var ids = await db.Users.OnlySelectable().OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();

        Assert.Equal(Expected(r => r.Selectable), ids);
    }

    [Fact]
    public async Task OnlyListable_ReturnsExactlyTheExpectedAgents()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var ids = await db.Users.OnlyListable().OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();

        Assert.Equal(Expected(r => r.Listable), ids);
    }

    [Fact]
    public async Task OnlyAuditFilterable_ReturnsExactlyTheExpectedAgents()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var ids = await db.Users.OnlyAuditFilterable().OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();

        Assert.Equal(Expected(r => r.AuditFilterable), ids);
    }

    [Fact]
    public async Task OnlyAuditFilterable_ListsTeamLeadsAndPartnersOnPurpose()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var ids = await db.Users.OnlyAuditFilterable().Select(u => u.Id).ToListAsync();

        // the audit viewer exists to inspect exactly these accounts' log rows
        Assert.Contains("teamlead", ids);
        Assert.Contains("partner", ids);
        Assert.Contains("partner-teamlead", ids);
        Assert.DoesNotContain("pending-blank", ids);
        Assert.DoesNotContain("applicant", ids);
    }

    [Fact]
    public async Task OnlyListable_KeepsTerminatedAndBlocked_ButNeverBlankCodenames()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var ids = await db.Users.OnlyListable().Select(u => u.Id).ToListAsync();

        // the whole reason there are two predicates: a former agent's past log rows must stay filterable
        Assert.Contains("terminated", ids);
        Assert.Contains("blocked", ids);
        Assert.DoesNotContain("pending-blank", ids);
        Assert.DoesNotContain("applicant", ids);
    }

    [Fact]
    public async Task NeitherPredicate_EverReturnsATeamLead()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var selectable = await db.Users.OnlySelectable().Select(u => u.Id).ToListAsync();
        var listable = await db.Users.OnlyListable().Select(u => u.Id).ToListAsync();

        foreach (var id in new[] { "teamlead", "teamlead-admin", "partner-teamlead" })
        {
            Assert.DoesNotContain(id, selectable);
            Assert.DoesNotContain(id, listable);
        }
    }

    [Fact]
    public async Task NeitherPredicate_EverReturnsAPartner()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var selectable = await db.Users.OnlySelectable().Select(u => u.Id).ToListAsync();
        var listable = await db.Users.OnlyListable().Select(u => u.Id).ToListAsync();

        foreach (var id in new[] { "partner", "partner-teamlead" })
        {
            Assert.DoesNotContain(id, selectable);
            Assert.DoesNotContain(id, listable);
        }
    }

    [Fact]
    public async Task OnlyWithPersonnelFile_ReturnsExactlyTheExpectedAgents()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var ids = await db.Users.OnlyWithPersonnelFile().OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();

        Assert.Equal(Expected(r => r.PersonnelFile), ids);
    }

    [Fact]
    public async Task NoPredicate_EverReturnsACitizen()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        // the public area shares the identity table; a citizen must never surface as agency staff
        Assert.DoesNotContain("civilian", await db.Users.OnlySelectable().Select(u => u.Id).ToListAsync());
        Assert.DoesNotContain("civilian", await db.Users.OnlyListable().Select(u => u.Id).ToListAsync());
        Assert.DoesNotContain("civilian", await db.Users.OnlyAuditFilterable().Select(u => u.Id).ToListAsync());
        Assert.DoesNotContain("civilian", await db.Users.OnlyWithPersonnelFile().Select(u => u.Id).ToListAsync());
    }

    [Fact]
    public async Task HasPersonnelFile_MirrorsOnlyWithPersonnelFile()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var fromDb = await db.Users.OnlyWithPersonnelFile().OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();
        var all = await db.Users.OrderBy(u => u.Id).ToListAsync();

        Assert.Equal(fromDb, all.Where(AgentSelection.HasPersonnelFile).Select(a => a.Id).ToList());
    }

    [Fact]
    public async Task IsSelectable_MirrorsOnlySelectable()
    {
        using var ctx = await SeededAsync();
        await using var db = ctx.NewContext();

        var fromDb = await db.Users.OnlySelectable().OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();
        var all = await db.Users.OrderBy(u => u.Id).ToListAsync();

        // drift guard between the expression and its compiled twin
        Assert.Equal(fromDb, all.Where(AgentSelection.IsSelectable).Select(a => a.Id).ToList());
    }
}
