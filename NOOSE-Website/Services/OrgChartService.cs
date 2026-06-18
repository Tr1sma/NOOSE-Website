using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.OrgChart;

namespace NOOSE_Website.Services;

/// <summary>Assembles org-chart data in flat queries (no N+1, no CROSS APPLY on MySQL).</summary>
public class OrgChartService(IDbContextFactory<AppDbContext> dbFactory) : IOrgChartService
{
    public async Task<OrgChartData> GetAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // active staff only; team leads are RP-invisible and rankless agents have no level
        var roster = await db.Users.AsNoTracking()
            .Where(a => a.Status == AgentStatus.Active && !a.IsTeamLead && a.Rank != null)
            .OrderBy(a => a.Codename)
            .ToListAsync(cancellationToken);

        var ranks = roster
            .GroupBy(a => a.Rank!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => new RankGroup(g.Key, g.ToList()))
            .ToList();

        var tru = roster.Where(a => a.IsTRU).ToList();
        var hrb = roster.Where(a => a.IsHRB).ToList();

        // visible and approved taskforces only
        var mayAllTf = viewer.MayAllTaskforcesSee();
        var meId = viewer.GetAgentId();
        var taskforces = await db.Taskforces.AsNoTracking()
            .OnlyVisible(db, mayAllTf, meId)
            .Where(t => t.Status == TaskforceStatus.Approved)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // load all members in one flat query, then group in memory
        var tfIds = taskforces.Select(t => t.Id).ToList();
        var members = tfIds.Count == 0
            ? new List<TaskforceAgent>()
            : await db.TaskforceAgents.AsNoTracking()
                .Where(ta => tfIds.Contains(ta.TaskforceId))
                .Include(ta => ta.Agent)
                .ToListAsync(cancellationToken);

        var staffings = taskforces
            .Select(t => new TaskforceStaffing(t,
                members
                    .Where(m => m.TaskforceId == t.Id && m.Agent != null && !m.Agent.IsTeamLead)
                    .OrderBy(m => m.Role == TaskforceRole.Member) // leads first
                    .ThenBy(m => m.Role)
                    .ThenBy(m => m.Agent!.Codename)
                    .ToList()))
            .ToList();

        return new OrgChartData(ranks, tru, hrb, staffings);
    }
}
