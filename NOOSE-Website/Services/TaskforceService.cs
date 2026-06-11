using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Taskforces;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITaskforceService" />
public class TaskforceService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen) : ITaskforceService
{
    public async Task<List<Taskforce>> GetListeAsync(bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taskforces
            .NurSichtbare(db, darfAlles, meId)
            .OrderByDescending(t => t.GeaendertAm ?? t.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Taskforce?> GetDetailAsync(string id, bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (taskforce is null)
        {
            return null;
        }
        // Sichtbar nur für Führung/Admin oder zugeteilte Agenten (Verschlusssache ist damit subsumiert).
        if (!darfAlles
            && !(meId is not null && await db.TaskforceAgenten.AnyAsync(a => a.TaskforceId == id && a.AgentId == meId, cancellationToken)))
        {
            return null;
        }
        return taskforce;
    }

    public async Task<List<Taskforce>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taskforces.IgnoreQueryFilters()
            .Where(t => t.IstGeloescht)
            .OrderByDescending(t => t.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Taskforce>> SucheAsync(string? suchtext, bool darfAlles, string? meId, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Taskforces.NurSichtbare(db, darfAlles, meId);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(t => t.Name.Contains(s) || t.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(t => t.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Taskforce>> GetBeantragteAsync(CancellationToken cancellationToken = default)
    {
        // Nur für den Führungs-Freigabe-Posteingang (Seite ist Policies.Fuehrung-gated) → kein VS-Filter nötig.
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taskforces
            .Where(t => t.Status == TaskforceStatus.Beantragt)
            .OrderBy(t => t.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Taskforce> ErstellenAsync(TaskforceEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var taskforce = new Taskforce
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "TF", cancellationToken),
            Name = eingabe.Name.Trim(),
            Zweck = Leer(eingabe.Zweck),
            Geltungsbereich = eingabe.Geltungsbereich,
            Status = TaskforceStatus.Beantragt,
            Bemerkungen = Leer(eingabe.Bemerkungen),
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };

        db.Taskforces.Add(taskforce);
        await db.SaveChangesAsync(cancellationToken);

        // Ersteller automatisch als Chefermittler (Leitung) zuteilen (so existiert stets mindestens eine Leitung).
        var erstellerId = handelnder.GetAgentId();
        if (erstellerId is not null)
        {
            db.TaskforceAgenten.Add(new TaskforceAgent
            {
                TaskforceId = taskforce.Id,
                AgentId = erstellerId,
                Rolle = TaskforceRolle.Chefermittler,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return taskforce;
    }

    public async Task AktualisierenAsync(string id, TaskforceEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");

        if (taskforce.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        taskforce.Name = eingabe.Name.Trim();
        taskforce.Zweck = Leer(eingabe.Zweck);
        taskforce.Geltungsbereich = eingabe.Geltungsbereich;
        taskforce.Bemerkungen = Leer(eingabe.Bemerkungen);
        taskforce.IstVerschlusssache = eingabe.IstVerschlusssache;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");
        db.Taskforces.Remove(taskforce);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");

        taskforce.IstGeloescht = false;
        taskforce.GeloeschtAm = null;
        taskforce.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task GenehmigungSetzenAsync(string id, TaskforceStatus neu, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Genehmigen/Ablehnen/Auflösen ist der Führung vorbehalten (Plan §6 „Taskforce genehmigen").
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");

        taskforce.Status = neu;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TaskforceAgent>> GetAgentenAsync(string taskforceId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TaskforceAgenten
            .Where(a => a.TaskforceId == taskforceId)
            .Include(a => a.Agent)
            // Leitung (Rolle != Mitglied) zuerst, dann nach Rolle (Chefermittler < CID-Lead < TRU-Lead), dann Codename.
            .OrderBy(a => a.Rolle == TaskforceRolle.Mitglied)
            .ThenBy(a => a.Rolle)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TaskforceAgent>> GetLeitungAsync(string taskforceId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TaskforceAgenten
            .Where(a => a.TaskforceId == taskforceId && a.Rolle != TaskforceRolle.Mitglied)
            .Include(a => a.Agent)
            .OrderBy(a => a.Rolle)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuteilenAsync(string taskforceId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == taskforceId, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{taskforceId}' nicht gefunden.");
        if (taskforce.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderLeitungAsync(db, taskforceId, handelnder, cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.TaskforceAgenten.AnyAsync(a => a.TaskforceId == taskforceId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Taskforce bereits zugeteilt.");
        }

        db.TaskforceAgenten.Add(new TaskforceAgent
        {
            TaskforceId = taskforceId,
            AgentId = agentId,
            Rolle = TaskforceRolle.Mitglied,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.TaskforceAgenten.Include(a => a.Taskforce).FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken);
        if (zuteilung is null)
        {
            return;
        }
        if (zuteilung.Taskforce?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderLeitungAsync(db, zuteilung.TaskforceId, handelnder, cancellationToken);
        db.TaskforceAgenten.Remove(zuteilung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RolleSetzenAsync(string zuteilungId, TaskforceRolle rolle, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Rollen/Leitung vergeben/entziehen ist der Führung vorbehalten.
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.TaskforceAgenten.FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        zuteilung.Rolle = rolle;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Wirft, wenn der Handelnde weder Führung noch Leitung (Rolle != Mitglied) dieser Taskforce ist.</summary>
    private static async Task VerlangeFuehrungOderLeitungAsync(AppDbContext db, string taskforceId, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var agentId = handelnder.GetAgentId();
        var istLeitung = agentId is not null && await db.TaskforceAgenten
            .AnyAsync(a => a.TaskforceId == taskforceId && a.AgentId == agentId && a.Rolle != TaskforceRolle.Mitglied, cancellationToken);
        if (!istLeitung)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder die Leitung dieser Taskforce.");
        }
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string taskforceId, bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Taskforce), taskforceId, darfAlles, cancellationToken, meId))
        {
            return new();
        }
        var agentZuteilungIds = await db.TaskforceAgenten
            .Where(a => a.TaskforceId == taskforceId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Beziehungen (Verknüpfungen), die diese Taskforce als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var beziehungIds = await db.Verknuepfungen
            .IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == nameof(Taskforce) && v.VonId == taskforceId)
                 || (v.NachTyp == nameof(Taskforce) && v.NachId == taskforceId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string> { taskforceId };
        ids.UnionWith(agentZuteilungIds);
        ids.UnionWith(beziehungIds);
        var typen = new[] { nameof(Taskforce), nameof(TaskforceAgent), nameof(Verknuepfung) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
