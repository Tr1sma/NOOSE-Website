using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Parteien;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IParteiService" />
public class ParteiService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen, ISteckbriefVorschlagService vorschlag, IPersonService personService) : IParteiService
{
    public async Task<List<Partei>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Mitglieder inkl. Person laden, damit die Listen-Mitgliederzahl exakt der Detailansicht entspricht.
        return await db.Parteien
            .Where(p => istFuehrung || !p.IstVerschlusssache)
            .Include(p => p.Mitglieder).ThenInclude(m => m.Person)
            .OrderByDescending(p => p.GeaendertAm ?? p.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Partei?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (partei is null || (partei.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return partei;
    }

    public async Task<List<Partei>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Parteien.IgnoreQueryFilters()
            .Where(p => p.IstGeloescht)
            .OrderByDescending(p => p.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Partei>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Parteien.Where(p => istFuehrung || !p.IstVerschlusssache);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(p => p.Name.Contains(s) || p.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Partei> ErstellenAsync(ParteiEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(eingabe.Einstufung, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var partei = new Partei
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "PT", cancellationToken),
            Name = eingabe.Name.Trim(),
            Beschreibung = Leer(eingabe.Beschreibung),
            Ziele = Leer(eingabe.Ziele),
            Bemerkungen = Leer(eingabe.Bemerkungen),
            Einstufung = eingabe.Einstufung,
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Partei), partei.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        db.Parteien.Add(partei);
        await db.SaveChangesAsync(cancellationToken);

        // Im Anlege-Formular erfasste Mitglieder übernehmen (bestehende Personen + automatisch angelegte
        // neue Akten, dedupliziert) und anschließend die Parteikollegen-Verknüpfungen aufbauen.
        if (eingabe.Mitglieder.Count > 0)
        {
            var bestehendeIds = eingabe.Mitglieder
                .Select(m => m.PersonId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var existierend = bestehendeIds.Count == 0
                ? new HashSet<string>()
                : (await db.Personen.Where(p => bestehendeIds.Contains(p.Id)).Select(p => p.Id)
                    .ToListAsync(cancellationToken)).ToHashSet();

            var hinzugefuegt = new List<string>();
            var gesehen = new HashSet<string>();
            var gesehenNeueNamen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in eingabe.Mitglieder)
            {
                string? pid = null;
                if (!string.IsNullOrWhiteSpace(m.PersonId) && existierend.Contains(m.PersonId))
                {
                    pid = m.PersonId;
                }
                else if (string.IsNullOrWhiteSpace(m.PersonId) && !string.IsNullOrWhiteSpace(m.NeuePersonName))
                {
                    // Derselbe neue Name im selben Formular → nur EINE Akte anlegen (keine Dubletten).
                    if (!gesehenNeueNamen.Add(m.NeuePersonName.Trim()))
                    {
                        continue;
                    }
                    var person = await personService.ErstellenAsync(new PersonEingabe { Name = m.NeuePersonName.Trim() }, handelnder, cancellationToken);
                    pid = person.Id;
                }
                if (pid is null || !gesehen.Add(pid))
                {
                    continue;
                }
                db.ParteiMitglieder.Add(new ParteiMitglied
                {
                    ParteiId = partei.Id,
                    PersonId = pid,
                    Rolle = Leer(m.Rolle),
                    IstLeitung = m.IstLeitung,
                });
                hinzugefuegt.Add(pid);
            }
            if (hinzugefuegt.Count > 0)
            {
                await VorschlaegeVormerkenAsync(db, partei.IstVerschlusssache, eingabe.Mitglieder.Select(m => m.Rolle), cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                foreach (var pid in hinzugefuegt)
                {
                    await ParteikollegenSyncAsync(db, pid, cancellationToken);
                }
            }
        }

        await tx.CommitAsync(cancellationToken);
        return partei;
    }

    public async Task AktualisierenAsync(string id, ParteiEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");

        if (partei.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        partei.Name = eingabe.Name.Trim();
        partei.Beschreibung = Leer(eingabe.Beschreibung);
        partei.Ziele = Leer(eingabe.Ziele);
        partei.Bemerkungen = Leer(eingabe.Bemerkungen);
        partei.IstVerschlusssache = eingabe.IstVerschlusssache;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");
        db.Parteien.Remove(partei);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");

        partei.IstGeloescht = false;
        partei.GeloeschtAm = null;
        partei.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(neu, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");

        if (partei.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        partei.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Partei), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Partei), id, istFuehrung, cancellationToken))
        {
            return new();
        }
        return await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == nameof(Partei) && e.EntitaetId == id)
            .OrderByDescending(e => e.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ParteiMitglied>> GetMitgliederAsync(string parteiId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglieder = await db.ParteiMitglieder
            .Where(m => m.ParteiId == parteiId)
            .Include(m => m.Person)
            .ToListAsync(cancellationToken);

        // Person == null → Akte im Papierkorb (Soft-Delete-Filter); ausblenden. Verschlusssache nur für Führung.
        return mitglieder
            .Where(m => m.Person is not null && (istFuehrung || !m.Person.IstVerschlusssache))
            .OrderByDescending(m => m.IstLeitung)
            .ThenBy(m => m.Person!.Name)
            .ToList();
    }

    public async Task MitgliedHinzufuegenAsync(string parteiId, ParteiMitgliedEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.FirstOrDefaultAsync(p => p.Id == parteiId, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{parteiId}' nicht gefunden.");
        if (partei.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var personId = await PersonIdErmittelnAsync(db, eingabe.PersonId, eingabe.NeuePersonName, handelnder, cancellationToken);
        if (await db.ParteiMitglieder.AnyAsync(m => m.ParteiId == parteiId && m.PersonId == personId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Person ist bereits Mitglied der Partei.");
        }

        // Mitgliedschaft + automatische Parteikollegen-Verknüpfungen in EINER Transaktion.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ParteiMitglieder.Add(new ParteiMitglied
        {
            ParteiId = parteiId,
            PersonId = personId,
            Rolle = Leer(eingabe.Rolle),
            IstLeitung = eingabe.IstLeitung,
        });
        await VorschlaegeVormerkenAsync(db, partei.IstVerschlusssache, new[] { eingabe.Rolle }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await ParteikollegenSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Liefert die Personen-Id: bestehende (mit Existenzprüfung) oder – bei nur neuem Namen – eine frisch
    /// angelegte Personen-Akte (committet, eigenes Aktenzeichen).
    /// </summary>
    private Task<string> PersonIdErmittelnAsync(AppDbContext db, string? personId, string? neuerName, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
        => MitgliedHelfer.PersonIdErmittelnAsync(db, personService, personId, neuerName, handelnder, cancellationToken);

    public async Task MitgliedAendernAsync(string mitgliedId, string? rolle, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.ParteiMitglieder.Include(m => m.Partei).FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        if (mitglied.Partei?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        mitglied.Rolle = Leer(rolle);
        mitglied.IstLeitung = istLeitung;
        await VorschlaegeVormerkenAsync(db, mitglied.Partei?.IstVerschlusssache == true, new[] { rolle }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.ParteiMitglieder.Include(m => m.Partei).FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken);
        if (mitglied is null)
        {
            return;
        }
        if (mitglied.Partei?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        var personId = mitglied.PersonId;
        // Austritt + Kollegen-Verknüpfungen in EINER Transaktion. Soft-Delete (ISoftDelete): der Interceptor setzt
        // GeloeschtAm (= Austrittsdatum) statt hart zu löschen → Mitgliedschaft bleibt als Verlaufseintrag erhalten.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ParteiMitglieder.Remove(mitglied);
        await db.SaveChangesAsync(cancellationToken);
        await ParteikollegenSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<List<ParteiAgent>> GetAgentenAsync(string parteiId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ParteiAgenten
            .Where(a => a.ParteiId == parteiId)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuteilenAsync(string parteiId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var partei = await db.Parteien.FirstOrDefaultAsync(p => p.Id == parteiId, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{parteiId}' nicht gefunden.");
        if (partei.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.ParteiAgenten.AnyAsync(a => a.ParteiId == parteiId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Partei bereits zugeteilt.");
        }

        db.ParteiAgenten.Add(new ParteiAgent
        {
            ParteiId = parteiId,
            AgentId = agentId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.ParteiAgenten.Include(a => a.Partei).FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken);
        if (zuteilung is null)
        {
            return;
        }
        if (zuteilung.Partei?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        db.ParteiAgenten.Remove(zuteilung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string parteiId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Partei), parteiId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var mitgliedIds = await db.ParteiMitglieder
            .Where(m => m.ParteiId == parteiId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var agentZuteilungIds = await db.ParteiAgenten
            .Where(a => a.ParteiId == parteiId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Beziehungen (Konflikte/Bündnisse), die diese Partei als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var beziehungIds = await db.Verknuepfungen
            .IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == nameof(Partei) && v.VonId == parteiId)
                 || (v.NachTyp == nameof(Partei) && v.NachId == parteiId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(mitgliedIds) { parteiId };
        ids.UnionWith(agentZuteilungIds);
        ids.UnionWith(beziehungIds);
        var typen = new[] { nameof(Partei), nameof(ParteiMitglied), nameof(ParteiAgent), nameof(Verknuepfung) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Synchronisiert die automatischen „Parteikollege"-Verknüpfungen der Person (analog zu den
    /// Fraktions-/Gruppenkollegen): zwischen P und Q soll genau dann eine bestehen, wenn beide mindestens
    /// eine Partei teilen. Wird nach jeder Mitglieder-Änderung für die betroffene Person aufgerufen.
    /// </summary>
    private static async Task ParteikollegenSyncAsync(AppDbContext db, string personId, CancellationToken cancellationToken)
    {
        var meineParteien = await db.ParteiMitglieder
            .Where(m => m.PersonId == personId)
            .Select(m => m.ParteiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var soll = meineParteien.Count == 0
            ? new List<string>()
            : await db.ParteiMitglieder
                .Where(m => meineParteien.Contains(m.ParteiId) && m.PersonId != personId)
                .Select(m => m.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await KollegenSync.SyncAsync(db, personId, KollegenSync.Parteikollege, soll, cancellationToken);
    }

    /// <summary>
    /// Merkt eingegebene Mitglieds-Rollen im gemeinsamen Vorschlagskatalog vor (Autocomplete beim nächsten
    /// Mal), analog zu den Steckbrief-Feldern und der Fraktions-Art. Verschlusssachen bleiben außen vor,
    /// damit keine sensiblen Werte in die geteilte Liste gelangen. Nur vormerken – der Aufrufer speichert
    /// im selben SaveChanges (atomar mit der Mitgliedschaft).
    /// </summary>
    private async Task VorschlaegeVormerkenAsync(AppDbContext db, bool istVerschlusssache, IEnumerable<string?> rollen, CancellationToken cancellationToken)
    {
        if (istVerschlusssache)
        {
            return;
        }
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Parteirolle, rollen.Where(r => r is not null).Select(r => r!), cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
