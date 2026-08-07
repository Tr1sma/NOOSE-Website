using System.Linq.Expressions;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Services.Integration;

// scratch probe: does Where(expr) vs expr.Compile() have discriminating power?
public sealed class ZzTautologyProbeTests
{
    // ONE expression instance, exactly like AgentSelection.SelectableRule
    private static readonly Expression<Func<Agent, bool>> Rule =
        a => a.Status == AgentStatus.Active && !a.IsTeamLead && a.PartnerAgency == null
             && a.Codename.StartsWith("codename");

    // the compiled twin, exactly like AgentSelection.SelectableInMemory
    private static readonly Func<Agent, bool> InMemory = Rule.Compile();

    [Fact]
    public async Task SameExpressionInstance_CanStillDisagreeAcrossTheDbBoundary()
    {
        using var ctx = new SqliteTestContext();
        await using (var seed = ctx.NewContext())
        {
            seed.Users.Add(Seed.Agent("active"));
            seed.Users.Add(Seed.Agent("teamlead", configure: a => a.IsTeamLead = true));
            await seed.SaveChangesAsync();
        }

        await using var db = ctx.NewContext();
        var fromDb = await db.Users.Where(Rule).OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();
        var all = await db.Users.OrderBy(u => u.Id).ToListAsync();
        var fromMemory = all.Where(InMemory).Select(a => a.Id).ToList();

        // report both sides so the run output shows the divergence
        Assert.Equal($"db=[{string.Join(",", fromDb)}]", $"mem=[{string.Join(",", fromMemory)}]");
    }
}
