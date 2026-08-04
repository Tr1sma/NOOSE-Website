using System.Security.Claims;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.CounterIntel;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Tests for the counter-intelligence cockpit: pure insider-threat rules + the guarded service.</summary>
public sealed class CounterIntelTests
{
    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
    private static ClaimsPrincipal Junior() => ClaimsPrincipalBuilder.Agent("low").WithRank(Rank.JuniorAgent).Build();

    private static AccessRow Row(string agent, DateTime localWhen, string id)
        => new(agent, agent.ToUpperInvariant(), localWhen, "Person", id);

    // ==================== InsiderThreatRules (pure) ====================

    [Fact]
    public void Rules_FlagOffHours()
    {
        var at2am = new DateTime(2026, 8, 1, 2, 0, 0);
        var rows = Enumerable.Range(0, 20).Select(i => Row("a", at2am, $"p{i}")).ToList();

        Assert.Contains(InsiderThreatRules.Evaluate(rows), f => f.Rule == "Off-Hours");
    }

    [Fact]
    public void Rules_FlagMassAccess()
    {
        var noon = new DateTime(2026, 8, 1, 12, 0, 0);
        var rows = Enumerable.Range(0, 45).Select(i => Row("a", noon, $"p{i}")).ToList();

        Assert.Contains(InsiderThreatRules.Evaluate(rows), f => f.Rule == "Massen-Zugriff");
    }

    [Fact]
    public void Rules_FlagBurst_ButNotMass_ForRepeatedSameRecord()
    {
        var noon = new DateTime(2026, 8, 1, 12, 0, 0);
        var rows = Enumerable.Range(0, 32).Select(_ => Row("a", noon, "p1")).ToList();

        var flags = InsiderThreatRules.Evaluate(rows);
        Assert.Contains(flags, f => f.Rule == "Zugriffs-Burst");
        Assert.DoesNotContain(flags, f => f.Rule == "Massen-Zugriff");
    }

    [Fact]
    public void Rules_CleanUsage_NoFlags()
    {
        var noon = new DateTime(2026, 8, 1, 12, 0, 0);
        var rows = Enumerable.Range(0, 5).Select(i => Row("a", noon.AddMinutes(i), $"p{i}")).ToList();

        Assert.Empty(InsiderThreatRules.Evaluate(rows));
    }

    // ==================== CounterIntelService ====================

    [Fact]
    public async Task GetOverviewAsync_Throws_ForNonLeadership()
    {
        using var ctx = new SqliteTestContext();
        var svc = new CounterIntelService(ctx.Factory);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetOverviewAsync(Junior()));
    }

    [Fact]
    public async Task GetOverviewAsync_CountsRecentAccesses()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            for (var i = 0; i < 3; i++)
            {
                db.AccessLogs.Add(new AccessLog
                {
                    AgentId = "a1", AgentName = "A", Timestamp = DateTime.UtcNow.AddHours(-1),
                    EntityType = "Person", EntityId = $"p{i}",
                });
            }
            db.SaveChanges();
        }
        var overview = await new CounterIntelService(ctx.Factory).GetOverviewAsync(Leader());

        Assert.Equal(3, overview.TotalAccesses);
        Assert.Equal(1, overview.DistinctAgents);
        Assert.Equal(3, overview.DistinctRecords);
    }

    [Fact]
    public async Task GetFlagsAsync_ExcludesReadOnlySupervisors()
    {
        using var ctx = new SqliteTestContext();
        var noon = DateTime.UtcNow.Date.AddHours(12);
        using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent("reader", configure: a => a.IsTeamLead = true)); // OnlyReader
            for (var i = 0; i < 45; i++)
            {
                db.AccessLogs.Add(new AccessLog { AgentId = "normal", AgentName = "Normal", Timestamp = noon.AddSeconds(i), EntityType = "Person", EntityId = $"n{i}" });
                db.AccessLogs.Add(new AccessLog { AgentId = "reader", AgentName = "Reader", Timestamp = noon.AddSeconds(i), EntityType = "Person", EntityId = $"r{i}" });
            }
            db.SaveChanges();
        }
        var flags = await new CounterIntelService(ctx.Factory).GetFlagsAsync(Leader());

        Assert.Contains(flags, f => f.AgentId == "normal");
        Assert.DoesNotContain(flags, f => f.AgentId == "reader");
    }
}
