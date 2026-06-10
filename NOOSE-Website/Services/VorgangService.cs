using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Vorgaenge;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVorgangService" />
public class VorgangService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen, ISteckbriefVorschlagService vorschlag) : IVorgangService
{
    public async Task<List<Vorgang>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vorgaenge
            .Where(v => istFuehrung || !v.IstVerschlusssache)
            .OrderByDescending(v => v.GeaendertAm ?? v.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vorgang?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorgang = await db.Vorgaenge.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vorgang is null || (vorgang.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return vorgang;
    }

    public async Task<List<Vorgang>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vorgaenge.IgnoreQueryFilters()
            .Where(v => v.IstGeloescht)
            .OrderByDescending(v => v.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Vorgang>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Vorgaenge.Where(v => istFuehrung || !v.IstVerschlusssache);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(v => v.Titel.Contains(s) || v.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(v => v.Titel)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vorgang> ErstellenAsync(VorgangEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(eingabe.Einstufung, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var istAbgeschlossen = VorgangStatusAnzeige.IstAbgeschlossen(eingabe.Status);
        var vorgang = new Vorgang
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "V", cancellationToken),
            Titel = eingabe.Titel.Trim(),
            Typ = Leer(eingabe.Typ),
            Status = eingabe.Status,
            Beschreibung = Leer(eingabe.Beschreibung),
            Zusammenfassung = Leer(eingabe.Zusammenfassung),
            Abschlussvermerk = Leer(eingabe.Abschlussvermerk),
            AbgeschlossenAm = istAbgeschlossen ? DateTime.UtcNow : null,
            Einstufung = eingabe.Einstufung,
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Vorgang), vorgang.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        await VorschlaegeVormerkenAsync(db, vorgang.IstVerschlusssache, eingabe.Typ, cancellationToken);

        db.Vorgaenge.Add(vorgang);
        await db.SaveChangesAsync(cancellationToken);

        // Ersteller automatisch zuteilen und als Fallführer markieren (so existiert stets mindestens ein FF).
        var erstellerId = handelnder.GetAgentId();
        if (erstellerId is not null)
        {
            db.VorgangAgenten.Add(new VorgangAgent
            {
                VorgangId = vorgang.Id,
                AgentId = erstellerId,
                IstFallfuehrer = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return vorgang;
    }

    public async Task AktualisierenAsync(string id, VorgangEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorgang = await db.Vorgaenge.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");

        if (vorgang.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Abschluss-Zeitpunkt mit dem Statuswechsel pflegen: setzen beim Wechsel in einen Abschluss-Status,
        // wieder leeren, sobald der Vorgang erneut „offen" ist.
        var warAbgeschlossen = VorgangStatusAnzeige.IstAbgeschlossen(vorgang.Status);
        var istAbgeschlossen = VorgangStatusAnzeige.IstAbgeschlossen(eingabe.Status);

        vorgang.Titel = eingabe.Titel.Trim();
        vorgang.Typ = Leer(eingabe.Typ);
        vorgang.Status = eingabe.Status;
        vorgang.Beschreibung = Leer(eingabe.Beschreibung);
        vorgang.Zusammenfassung = Leer(eingabe.Zusammenfassung);
        vorgang.Abschlussvermerk = Leer(eingabe.Abschlussvermerk);
        vorgang.IstVerschlusssache = eingabe.IstVerschlusssache;

        if (istAbgeschlossen && !warAbgeschlossen)
        {
            vorgang.AbgeschlossenAm = DateTime.UtcNow;
        }
        else if (!istAbgeschlossen)
        {
            vorgang.AbgeschlossenAm = null;
        }

        await VorschlaegeVormerkenAsync(db, vorgang.IstVerschlusssache, eingabe.Typ, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorgang = await db.Vorgaenge.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");
        db.Vorgaenge.Remove(vorgang);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorgang = await db.Vorgaenge.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");

        vorgang.IstGeloescht = false;
        vorgang.GeloeschtAm = null;
        vorgang.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(neu, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorgang = await db.Vorgaenge.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");

        if (vorgang.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        vorgang.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Vorgang), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Vorgang), id, istFuehrung, cancellationToken))
        {
            return new();
        }
        return await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == nameof(Vorgang) && e.EntitaetId == id)
            .OrderByDescending(e => e.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VorgangAgent>> GetAgentenAsync(string vorgangId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.VorgangAgenten
            .Where(a => a.VorgangId == vorgangId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IstFallfuehrer)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VorgangAgent>> GetFallfuehrerAsync(string vorgangId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.VorgangAgenten
            .Where(a => a.VorgangId == vorgangId && a.IstFallfuehrer)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuteilenAsync(string vorgangId, string agentId, bool alsFallfuehrer, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorgang = await db.Vorgaenge.FirstOrDefaultAsync(v => v.Id == vorgangId, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{vorgangId}' nicht gefunden.");
        if (vorgang.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderFFAsync(db, vorgangId, handelnder, cancellationToken);
        // Das Fallführer-Flag darf nur die Führung vergeben (auch beim Zuteilen).
        if (alsFallfuehrer)
        {
            Berechtigung.VerlangeFuehrung(handelnder);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.VorgangAgenten.AnyAsync(a => a.VorgangId == vorgangId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist dem Vorgang bereits zugeteilt.");
        }

        db.VorgangAgenten.Add(new VorgangAgent
        {
            VorgangId = vorgangId,
            AgentId = agentId,
            IstFallfuehrer = alsFallfuehrer,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.VorgangAgenten.Include(a => a.Vorgang).FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken);
        if (zuteilung is null)
        {
            return;
        }
        if (zuteilung.Vorgang?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderFFAsync(db, zuteilung.VorgangId, handelnder, cancellationToken);
        db.VorgangAgenten.Remove(zuteilung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FallfuehrerSetzenAsync(string zuteilungId, bool ist, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Fallführer vergeben/entziehen ist der Führung vorbehalten.
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.VorgangAgenten.FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        zuteilung.IstFallfuehrer = ist;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Wirft, wenn der Handelnde weder Führung noch Fallführer dieses Vorgangs ist.</summary>
    private static async Task VerlangeFuehrungOderFFAsync(AppDbContext db, string vorgangId, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var agentId = handelnder.GetAgentId();
        var istFF = agentId is not null && await db.VorgangAgenten
            .AnyAsync(a => a.VorgangId == vorgangId && a.AgentId == agentId && a.IstFallfuehrer, cancellationToken);
        if (!istFF)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Fallführer dieser Akte.");
        }
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string vorgangId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Vorgang), vorgangId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var agentZuteilungIds = await db.VorgangAgenten
            .Where(a => a.VorgangId == vorgangId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Verknüpfungen (gebündelte Akten), die diesen Vorgang als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var beziehungIds = await db.Verknuepfungen
            .IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == nameof(Vorgang) && v.VonId == vorgangId)
                 || (v.NachTyp == nameof(Vorgang) && v.NachId == vorgangId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string> { vorgangId };
        ids.UnionWith(agentZuteilungIds);
        ids.UnionWith(beziehungIds);
        var typen = new[] { nameof(Vorgang), nameof(VorgangAgent), nameof(Verknuepfung) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Merkt den eingegebenen Vorgangs-Typ im gemeinsamen Vorschlagskatalog vor (Autocomplete beim nächsten
    /// Mal), analog zum Operations-Typ. Verschlusssachen bleiben außen vor, damit keine sensiblen Werte in die
    /// geteilte Liste gelangen. Nur vormerken – der Aufrufer speichert im selben SaveChanges (atomar).
    /// </summary>
    private async Task VorschlaegeVormerkenAsync(AppDbContext db, bool istVerschlusssache, string? typ, CancellationToken cancellationToken)
    {
        if (istVerschlusssache || string.IsNullOrWhiteSpace(typ))
        {
            return;
        }
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Vorgangstyp, new[] { typ }, cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
