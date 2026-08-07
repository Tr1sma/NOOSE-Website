using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="AgentSelection"/> over in-memory SQLite.</summary>
/// <remarks>
/// One fixed matrix, both predicates asserted by exact membership. Every agent option list in the app
/// routes through here, so a silent widening of either rule has to fail loudly in this file.
/// </remarks>
public sealed class AgentSelectionTests
{
    /// <summary>Every account shape the roster can hold, with the expected verdict of both predicates.</summary>
    private static readonly (string Id, AgentStatus Status, Action<Agent>? Configure, bool Selectable, bool Listable)[] Matrix =
    [
        ("active", AgentStatus.Active, null, true, true),
        // read-only supervision is RP-wide invisible, so it shows up in no list at all
        ("teamlead", AgentStatus.Active, a => a.IsTeamLead = true, false, false),
        // not even with the admin flag on top: the marker itself hides the account
        ("teamlead-admin", AgentStatus.Active, a => { a.IsTeamLead = true; a.IsAdmin = true; }, false, false),
        ("partner", AgentStatus.Active, a => a.PartnerAgency = PartnerAgency.LSPD, false, false),
        ("partner-teamlead", AgentStatus.Active, a => { a.PartnerAgency = PartnerAgency.DoJ; a.IsTeamLead = true; }, false, false),
        // blank codename = never released, so there is no agent to name
        ("pending-blank", AgentStatus.Pending, a => a.Codename = string.Empty, false, false),
        ("pending-named", AgentStatus.Pending, null, false, true),
        ("blocked", AgentStatus.Blocked, null, false, true),
        ("terminated", AgentStatus.Terminated, null, false, true),
        ("applicant", AgentStatus.Applicant, a => a.Codename = string.Empty, false, false),
    ];

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

    private static string[] Expected(Func<(string Id, AgentStatus Status, Action<Agent>? Configure, bool Selectable, bool Listable), bool> pick)
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
