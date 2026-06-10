using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Operationen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IOperationService" />
public class OperationService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen, ISteckbriefVorschlagService vorschlag) : IOperationService
{
    public async Task<List<Operation>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Operationen
            .Where(o => istFuehrung || !o.IstVerschlusssache)
            .OrderByDescending(o => o.GeaendertAm ?? o.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Operation?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Operationen.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (operation is null || (operation.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return operation;
    }

    public async Task<List<Operation>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Operationen.IgnoreQueryFilters()
            .Where(o => o.IstGeloescht)
            .OrderByDescending(o => o.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Operation>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Operationen.Where(o => istFuehrung || !o.IstVerschlusssache);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(o => o.Titel.Contains(s) || o.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(o => o.Titel)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Operation> ErstellenAsync(OperationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(eingabe.Einstufung, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var operation = new Operation
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "OP", cancellationToken),
            Titel = eingabe.Titel.Trim(),
            Typ = Leer(eingabe.Typ),
            Status = eingabe.Status,
            Ort = Leer(eingabe.Ort),
            Beginn = eingabe.Beginn,
            Ende = eingabe.Ende,
            Ablauf = Leer(eingabe.Ablauf),
            Ergebnis = Leer(eingabe.Ergebnis),
            Bemerkungen = Leer(eingabe.Bemerkungen),
            Einstufung = eingabe.Einstufung,
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Operation), operation.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        await VorschlaegeVormerkenAsync(db, operation.IstVerschlusssache, eingabe.Typ, cancellationToken);

        db.Operationen.Add(operation);
        await db.SaveChangesAsync(cancellationToken);

        // Ersteller automatisch zuteilen und als Ermittlungsleiter markieren (so existiert stets mindestens ein EL).
        var erstellerId = handelnder.GetAgentId();
        if (erstellerId is not null)
        {
            db.OperationAgenten.Add(new OperationAgent
            {
                OperationId = operation.Id,
                AgentId = erstellerId,
                IstErmittlungsleiter = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return operation;
    }

    public async Task AktualisierenAsync(string id, OperationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Operationen.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Operation '{id}' nicht gefunden.");

        if (operation.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        operation.Titel = eingabe.Titel.Trim();
        operation.Typ = Leer(eingabe.Typ);
        operation.Status = eingabe.Status;
        operation.Ort = Leer(eingabe.Ort);
        operation.Beginn = eingabe.Beginn;
        operation.Ende = eingabe.Ende;
        operation.Ablauf = Leer(eingabe.Ablauf);
        operation.Ergebnis = Leer(eingabe.Ergebnis);
        operation.Bemerkungen = Leer(eingabe.Bemerkungen);
        operation.IstVerschlusssache = eingabe.IstVerschlusssache;

        await VorschlaegeVormerkenAsync(db, operation.IstVerschlusssache, eingabe.Typ, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Operationen.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Operation '{id}' nicht gefunden.");
        db.Operationen.Remove(operation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Operationen.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Operation '{id}' nicht gefunden.");

        operation.IstGeloescht = false;
        operation.GeloeschtAm = null;
        operation.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(neu, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Operationen.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Operation '{id}' nicht gefunden.");

        if (operation.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        operation.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Operation), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Operation), id, istFuehrung, cancellationToken))
        {
            return new();
        }
        return await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == nameof(Operation) && e.EntitaetId == id)
            .OrderByDescending(e => e.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OperationAgent>> GetAgentenAsync(string operationId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OperationAgenten
            .Where(a => a.OperationId == operationId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IstErmittlungsleiter)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OperationAgent>> GetErmittlungsleiterAsync(string operationId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OperationAgenten
            .Where(a => a.OperationId == operationId && a.IstErmittlungsleiter)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuteilenAsync(string operationId, string agentId, bool alsErmittlungsleiter, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Operationen.FirstOrDefaultAsync(o => o.Id == operationId, cancellationToken)
            ?? throw new InvalidOperationException($"Operation '{operationId}' nicht gefunden.");
        if (operation.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderELAsync(db, operationId, handelnder, cancellationToken);
        // Das Ermittlungsleiter-Flag darf nur die Führung vergeben (auch beim Zuteilen).
        if (alsErmittlungsleiter)
        {
            Berechtigung.VerlangeFuehrung(handelnder);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.OperationAgenten.AnyAsync(a => a.OperationId == operationId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Operation bereits zugeteilt.");
        }

        db.OperationAgenten.Add(new OperationAgent
        {
            OperationId = operationId,
            AgentId = agentId,
            IstErmittlungsleiter = alsErmittlungsleiter,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.OperationAgenten.Include(a => a.Operation).FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken);
        if (zuteilung is null)
        {
            return;
        }
        if (zuteilung.Operation?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderELAsync(db, zuteilung.OperationId, handelnder, cancellationToken);
        db.OperationAgenten.Remove(zuteilung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ErmittlungsleiterSetzenAsync(string zuteilungId, bool ist, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Ermittlungsleiter vergeben/entziehen ist der Führung vorbehalten.
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.OperationAgenten.FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        zuteilung.IstErmittlungsleiter = ist;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Wirft, wenn der Handelnde weder Führung noch Ermittlungsleiter dieser Operation ist.</summary>
    private static async Task VerlangeFuehrungOderELAsync(AppDbContext db, string operationId, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var agentId = handelnder.GetAgentId();
        var istEL = agentId is not null && await db.OperationAgenten
            .AnyAsync(a => a.OperationId == operationId && a.AgentId == agentId && a.IstErmittlungsleiter, cancellationToken);
        if (!istEL)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Ermittlungsleiter dieser Akte.");
        }
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string operationId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Operation), operationId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var agentZuteilungIds = await db.OperationAgenten
            .Where(a => a.OperationId == operationId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Beziehungen (Beteiligte/Verknüpfungen), die diese Operation als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var beziehungIds = await db.Verknuepfungen
            .IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == nameof(Operation) && v.VonId == operationId)
                 || (v.NachTyp == nameof(Operation) && v.NachId == operationId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string> { operationId };
        ids.UnionWith(agentZuteilungIds);
        ids.UnionWith(beziehungIds);
        var typen = new[] { nameof(Operation), nameof(OperationAgent), nameof(Verknuepfung) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Merkt den eingegebenen Operations-Typ im gemeinsamen Vorschlagskatalog vor (Autocomplete beim nächsten
    /// Mal), analog zur Fraktions-Art und der Partei-Rolle. Verschlusssachen bleiben außen vor, damit keine
    /// sensiblen Werte in die geteilte Liste gelangen. Nur vormerken – der Aufrufer speichert im selben
    /// SaveChanges (atomar mit der Operation).
    /// </summary>
    private async Task VorschlaegeVormerkenAsync(AppDbContext db, bool istVerschlusssache, string? typ, CancellationToken cancellationToken)
    {
        if (istVerschlusssache || string.IsNullOrWhiteSpace(typ))
        {
            return;
        }
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Operationstyp, new[] { typ }, cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
