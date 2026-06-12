using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Organigramm;

namespace NOOSE_Website.Services;

/// <summary>
/// Liest die Organigramm-Daten in drei flachen Abfragen zusammen (kein N+1, kein SelectMany/CROSS APPLY).
/// Roster = aktive Agenten ohne RP-unsichtbare Teamleitung und mit gesetztem Dienstgrad; gruppiert nach
/// Dienstgrad (Director→Junior). TRU = Querschnitt des Rosters. Taskforces = nur sichtbare (Taskforce-
/// Sichtbarkeit) und genehmigte, Mitglieder über eine einzige flache <c>WHERE TaskforceId IN (…)</c>-Abfrage.
/// </summary>
public class OrganigrammService(IDbContextFactory<AppDbContext> dbFactory) : IOrganigrammService
{
    public async Task<OrganigrammDaten> GetAsync(ClaimsPrincipal betrachter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Roster: aktives NOOSE-Personal. Teamleitung (FiveM-Aufsicht) ist RP-unsichtbar → überall ausblenden.
        // Agenten ohne Dienstgrad (noch nicht freigegeben/halb-migriert) gehören in keine Rang-Ebene.
        var roster = await db.Users.AsNoTracking()
            .Where(a => a.Status == AgentStatus.Aktiv && !a.IstTeamLeitung && a.Dienstgrad != null)
            .OrderBy(a => a.Codename)
            .ToListAsync(cancellationToken);

        // Nach Dienstgrad gruppieren, höchster Rang zuerst (Director oben → Junior unten).
        var raenge = roster
            .GroupBy(a => a.Dienstgrad!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => new RangGruppe(g.Key, g.ToList()))
            .ToList();

        var tru = roster.Where(a => a.IstTRU).ToList();

        // Nur sichtbare (zugeteilte bzw. Führung sieht alle) UND genehmigte Taskforces.
        var darfAlleTf = betrachter.DarfAlleTaskforcesSehen();
        var meId = betrachter.GetAgentId();
        var taskforces = await db.Taskforces.AsNoTracking()
            .NurSichtbare(db, darfAlleTf, meId)
            .Where(t => t.Status == TaskforceStatus.Genehmigt)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // Besetzung in EINER flachen Abfrage laden (kein N+1), dann in-memory je Taskforce gruppieren.
        var tfIds = taskforces.Select(t => t.Id).ToList();
        var mitglieder = tfIds.Count == 0
            ? new List<TaskforceAgent>()
            : await db.TaskforceAgenten.AsNoTracking()
                .Where(ta => tfIds.Contains(ta.TaskforceId))
                .Include(ta => ta.Agent)
                .ToListAsync(cancellationToken);

        var besetzungen = taskforces
            .Select(t => new TaskforceBesetzung(t,
                mitglieder
                    .Where(m => m.TaskforceId == t.Id && m.Agent != null && !m.Agent.IstTeamLeitung)
                    .OrderBy(m => m.Rolle == TaskforceRolle.Mitglied) // Leitung zuerst
                    .ThenBy(m => m.Rolle)
                    .ThenBy(m => m.Agent!.Codename)
                    .ToList()))
            .ToList();

        return new OrganigrammDaten(raenge, tru, besetzungen);
    }
}
