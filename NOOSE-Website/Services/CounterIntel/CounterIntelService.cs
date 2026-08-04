using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.CounterIntel;

namespace NOOSE_Website.Services;

/// <summary>Counter-intelligence cockpit: read-only aggregation over the access log. Leadership-only (not read-only supervisors).</summary>
public interface ICounterIntelService
{
    Task<CounterIntelOverview> GetOverviewAsync(ClaimsPrincipal actor, int days = 30, CancellationToken cancellationToken = default);
    Task<CounterIntelHeatmap> GetHeatmapAsync(ClaimsPrincipal actor, int days = 30, CancellationToken cancellationToken = default);
    Task<AgentAccessProfile?> GetAgentProfileAsync(ClaimsPrincipal actor, string agentId, int days = 30, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InsiderFlag>> GetFlagsAsync(ClaimsPrincipal actor, int days = 30, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentOption>> GetAgentsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ICounterIntelService" />
public class CounterIntelService(IDbContextFactory<AppDbContext> dbFactory) : ICounterIntelService
{
    private const int MaxRows = 30000;
    private const int MaxHeatmapAgents = 30;
    private const int RecentAccessCount = 25;

    public async Task<CounterIntelOverview> GetOverviewAsync(ClaimsPrincipal actor, int days = 30, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await LoadAsync(db, days, cancellationToken);
        return new CounterIntelOverview(
            rows.Count,
            rows.Select(r => r.AgentId).Distinct().Count(),
            rows.Select(r => $"{r.EntityType}:{r.EntityId}").Distinct().Count(),
            rows.Count(r => InsiderThreatRules.IsOffHours(r.LocalTimestamp)),
            days);
    }

    public async Task<CounterIntelHeatmap> GetHeatmapAsync(ClaimsPrincipal actor, int days = 30, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await LoadAsync(db, days, cancellationToken);

        var agents = rows.GroupBy(r => r.AgentId)
            .Select(g =>
            {
                var hours = new int[24];
                foreach (var r in g)
                {
                    hours[r.LocalTimestamp.Hour]++;
                }
                return new HeatAgent(g.Key, NameOr(g.First().AgentName), hours);
            })
            .OrderByDescending(a => a.Hours.Sum())
            .Take(MaxHeatmapAgents)
            .ToList();

        var max = agents.SelectMany(a => a.Hours).DefaultIfEmpty(0).Max();
        return new CounterIntelHeatmap(agents, max);
    }

    public async Task<AgentAccessProfile?> GetAgentProfileAsync(ClaimsPrincipal actor, string agentId, int days = 30, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = (await LoadAsync(db, days, cancellationToken)).Where(r => r.AgentId == agentId).ToList();
        if (rows.Count == 0)
        {
            return null;
        }
        var byType = rows.GroupBy(r => r.EntityType)
            .Select(g => new TypeCount(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ToList();
        var recent = rows.OrderByDescending(r => r.LocalTimestamp)
            .Take(RecentAccessCount)
            .Select(r => new RecentAccess(r.LocalTimestamp, r.EntityType, r.EntityId, Href(r.EntityType, r.EntityId)))
            .ToList();
        return new AgentAccessProfile(
            agentId, NameOr(rows[0].AgentName), rows.Count,
            rows.Select(r => $"{r.EntityType}:{r.EntityId}").Distinct().Count(),
            rows.Count(r => InsiderThreatRules.IsOffHours(r.LocalTimestamp)),
            byType, recent);
    }

    public async Task<IReadOnlyList<InsiderFlag>> GetFlagsAsync(ClaimsPrincipal actor, int days = 30, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await LoadAsync(db, days, cancellationToken);
        return InsiderThreatRules.Evaluate(rows);
    }

    public async Task<IReadOnlyList<AgentOption>> GetAgentsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var excluded = await OnlyReaderIdsAsync(db, cancellationToken);
        var rows = await db.AccessLogs.AsNoTracking()
            .Where(a => a.AgentId != null && a.AgentName != null)
            .Select(a => new { a.AgentId, a.AgentName })
            .Distinct().Take(500)
            .ToListAsync(cancellationToken);
        return rows
            .Where(a => a.AgentId is not null && !excluded.Contains(a.AgentId!))
            .GroupBy(a => a.AgentId!)
            .Select(g => new AgentOption(g.Key, g.First().AgentName ?? g.Key))
            .OrderBy(a => a.Name)
            .ToList();
    }

    // recent access rows, system rows + read-only supervisors removed, timestamps localized
    private static async Task<List<AccessRow>> LoadAsync(AppDbContext db, int days, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var excluded = await OnlyReaderIdsAsync(db, ct);
        var rows = await db.AccessLogs.AsNoTracking()
            .Where(a => a.AgentId != null && a.Timestamp >= since)
            .OrderByDescending(a => a.Timestamp)
            .Take(MaxRows)
            .Select(a => new { a.AgentId, a.AgentName, a.Timestamp, a.EntityType, a.EntityId })
            .ToListAsync(ct);
        return rows
            .Where(r => !excluded.Contains(r.AgentId!))
            .Select(r => new AccessRow(r.AgentId!, r.AgentName, r.Timestamp.ToLocalTime(), r.EntityType, r.EntityId))
            .ToList();
    }

    private static async Task<HashSet<string>> OnlyReaderIdsAsync(AppDbContext db, CancellationToken ct)
        => (await db.Users.Where(u => u.IsTeamLead && !u.IsAdmin).Select(u => u.Id).ToListAsync(ct)).ToHashSet();

    // never surface a raw agent id as a name
    private static string NameOr(string? name) => string.IsNullOrWhiteSpace(name) ? "(unbenannt)" : name;

    private static string? Href(string type, string id) => type switch
    {
        nameof(NOOSE_Website.Data.Entities.People.Person) => $"/personen/{id}",
        nameof(NOOSE_Website.Data.Entities.Factions.Faction) => $"/fraktionen/{id}",
        nameof(NOOSE_Website.Data.Entities.Groups.PersonGroup) => $"/personengruppen/{id}",
        nameof(NOOSE_Website.Data.Entities.Parties.Party) => $"/parteien/{id}",
        nameof(NOOSE_Website.Data.Entities.Operations.Operation) => $"/operationen/{id}",
        nameof(NOOSE_Website.Data.Entities.Cases.Case) => $"/vorgaenge/{id}",
        nameof(NOOSE_Website.Data.Entities.Taskforces.Taskforce) => $"/taskforces/{id}",
        _ => null,
    };
}
