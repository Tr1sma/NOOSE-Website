using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.OrgChart;

namespace NOOSE_Website.Services;

/// <summary>
/// Liest die Organigramm-Daten in drei flachen Abfragen zusammen (kein N+1, kein SelectMany/CROSS APPLY).
/// Roster = aktive Agenten ohne RP-unsichtbare Teamleitung und mit gesetztem Dienstgrad; gruppiert nach
/// Dienstgrad (Director→Junior). TRU/HRB = Querschnitt des Rosters. Taskforces = nur sichtbare (Taskforce-
/// Sichtbarkeit) und genehmigte, Mitglieder über eine einzige flache <c>WHERE TaskforceId IN (…)</c>-Abfrage.
/// </summary>
public class OrgChartService(IDbContextFactory<AppDbContext> dbFactory) : IOrgChartService
{
    public async Task<OrgChartData> GetAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Roster: aktives NOOSE-Personal. Teamleitung (FiveM-Aufsicht) ist RP-unsichtbar → überall ausblenden.
        // Agenten ohne Dienstgrad (noch nicht freigegeben/halb-migriert) gehören in keine Rang-Ebene.
        var roster = await db.Users.AsNoTracking()
            .Where(a => a.Status == AgentStatus.Active && !a.IsTeamLead && a.Rank != null)
            .OrderBy(a => a.Codename)
            .ToListAsync(cancellationToken);

        // Nach Dienstgrad gruppieren, höchster Rang zuerst (Director oben → Junior unten).
        var ranks = roster
            .GroupBy(a => a.Rank!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => new RankGroup(g.Key, g.ToList()))
            .ToList();

        var tru = roster.Where(a => a.IsTRU).ToList();
        var hrb = roster.Where(a => a.IsHRB).ToList();

        // Nur sichtbare (zugeteilte bzw. Führung sieht alle) UND genehmigte Taskforces.
        var mayAllTf = viewer.MayAllTaskforcesSee();
        var meId = viewer.GetAgentId();
        var taskforces = await db.Taskforces.AsNoTracking()
            .OnlyVisible(db, mayAllTf, meId)
            .Where(t => t.Status == TaskforceStatus.Approved)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // Besetzung in EINER flachen Abfrage laden (kein N+1), dann in-memory je Taskforce gruppieren.
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
                    .OrderBy(m => m.Role == TaskforceRole.Member) // Leitung zuerst
                    .ThenBy(m => m.Role)
                    .ThenBy(m => m.Agent!.Codename)
                    .ToList()))
            .ToList();

        return new OrgChartData(ranks, tru, hrb, staffings);
    }
}
