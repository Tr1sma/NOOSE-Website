using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Gruppen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonengruppeService" />
public class PersonengruppeService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen, IPersonService personService) : IPersonengruppeService
{
    public async Task<List<Personengruppe>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Personengruppen
            .Where(g => istFuehrung || !g.IstVerschlusssache)
            .Include(g => g.Mitglieder)
            .OrderByDescending(g => g.GeaendertAm ?? g.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Personengruppe?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gruppe = await db.Personengruppen.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (gruppe is null || (gruppe.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return gruppe;
    }

    public async Task<List<Personengruppe>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Personengruppen.IgnoreQueryFilters()
            .Where(g => g.IstGeloescht)
            .OrderByDescending(g => g.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Personengruppe>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Personengruppen.Where(g => istFuehrung || !g.IstVerschlusssache);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(g => g.Name.Contains(s) || g.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(g => g.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Personengruppe> ErstellenAsync(PersonengruppeEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(eingabe.Einstufung, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var gruppe = new Personengruppe
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "G", cancellationToken),
            Name = eingabe.Name.Trim(),
            Beschreibung = Leer(eingabe.Beschreibung),
            Ziele = Leer(eingabe.Ziele),
            Einstufung = eingabe.Einstufung,
            GeschaetzteMitgliederzahl = eingabe.GeschaetzteMitgliederzahl,
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Personengruppe), gruppe.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        db.Personengruppen.Add(gruppe);
        await db.SaveChangesAsync(cancellationToken);

        // Im Anlege-Formular erfasste Mitglieder übernehmen (bestehende Personen + automatisch angelegte
        // neue Akten, dedupliziert) und anschließend die Gruppenkollegen-Verknüpfungen aufbauen.
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
                db.PersonengruppeMitglieder.Add(new PersonengruppeMitglied
                {
                    PersonengruppeId = gruppe.Id,
                    PersonId = pid,
                    Rolle = Leer(m.Rolle),
                    IstLeitung = m.IstLeitung,
                });
                hinzugefuegt.Add(pid);
            }
            if (hinzugefuegt.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                foreach (var pid in hinzugefuegt)
                {
                    await GruppenkollegenSyncAsync(db, pid, cancellationToken);
                }
            }
        }

        await tx.CommitAsync(cancellationToken);
        return gruppe;
    }

    public async Task AktualisierenAsync(string id, PersonengruppeEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gruppe = await db.Personengruppen.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");

        gruppe.Name = eingabe.Name.Trim();
        gruppe.Beschreibung = Leer(eingabe.Beschreibung);
        gruppe.Ziele = Leer(eingabe.Ziele);
        gruppe.GeschaetzteMitgliederzahl = eingabe.GeschaetzteMitgliederzahl;
        gruppe.IstVerschlusssache = eingabe.IstVerschlusssache;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gruppe = await db.Personengruppen.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");
        db.Personengruppen.Remove(gruppe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gruppe = await db.Personengruppen.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");

        gruppe.IstGeloescht = false;
        gruppe.GeloeschtAm = null;
        gruppe.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(neu, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gruppe = await db.Personengruppen.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");

        gruppe.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Personengruppe), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == nameof(Personengruppe) && e.EntitaetId == id)
            .OrderByDescending(e => e.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonengruppeMitglied>> GetMitgliederAsync(string gruppeId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglieder = await db.PersonengruppeMitglieder
            .Where(m => m.PersonengruppeId == gruppeId)
            .Include(m => m.Person)
            .ToListAsync(cancellationToken);

        // Person == null → Akte im Papierkorb (Soft-Delete-Filter); ausblenden. Verschlusssache nur für Führung.
        return mitglieder
            .Where(m => m.Person is not null && (istFuehrung || !m.Person.IstVerschlusssache))
            .OrderByDescending(m => m.IstLeitung)
            .ThenBy(m => m.Person!.Name)
            .ToList();
    }

    public async Task MitgliedHinzufuegenAsync(string gruppeId, GruppeMitgliedEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Personengruppen.AnyAsync(g => g.Id == gruppeId, cancellationToken))
        {
            throw new InvalidOperationException($"Personengruppe '{gruppeId}' nicht gefunden.");
        }

        var personId = await PersonIdErmittelnAsync(db, eingabe.PersonId, eingabe.NeuePersonName, handelnder, cancellationToken);
        if (await db.PersonengruppeMitglieder.AnyAsync(m => m.PersonengruppeId == gruppeId && m.PersonId == personId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Person ist bereits Mitglied der Gruppe.");
        }

        // Mitgliedschaft + automatische Gruppenkollegen-Verknüpfungen in EINER Transaktion.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PersonengruppeMitglieder.Add(new PersonengruppeMitglied
        {
            PersonengruppeId = gruppeId,
            PersonId = personId,
            Rolle = Leer(eingabe.Rolle),
            IstLeitung = eingabe.IstLeitung,
        });
        await db.SaveChangesAsync(cancellationToken);
        await GruppenkollegenSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Liefert die Personen-Id: bestehende (mit Existenzprüfung) oder – bei nur neuem Namen – eine frisch
    /// angelegte Personen-Akte (committet, eigenes Aktenzeichen).
    /// </summary>
    private async Task<string> PersonIdErmittelnAsync(AppDbContext db, string? personId, string? neuerName, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(personId) && !string.IsNullOrWhiteSpace(neuerName))
        {
            var person = await personService.ErstellenAsync(new PersonEingabe { Name = neuerName.Trim() }, handelnder, cancellationToken);
            return person.Id;
        }
        if (string.IsNullOrWhiteSpace(personId) || !await db.Personen.AnyAsync(p => p.Id == personId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Person wurde nicht gefunden.");
        }
        return personId;
    }

    public async Task MitgliedAendernAsync(string mitgliedId, string? rolle, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.PersonengruppeMitglieder.FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        mitglied.Rolle = Leer(rolle);
        mitglied.IstLeitung = istLeitung;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.PersonengruppeMitglieder.FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken);
        if (mitglied is null)
        {
            return;
        }
        var personId = mitglied.PersonId;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PersonengruppeMitglieder.Remove(mitglied);
        await db.SaveChangesAsync(cancellationToken);
        await GruppenkollegenSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<List<PersonengruppeAgent>> GetAgentenAsync(string gruppeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonengruppeAgenten
            .Where(a => a.PersonengruppeId == gruppeId)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuteilenAsync(string gruppeId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Personengruppen.AnyAsync(g => g.Id == gruppeId, cancellationToken))
        {
            throw new InvalidOperationException($"Personengruppe '{gruppeId}' nicht gefunden.");
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.PersonengruppeAgenten.AnyAsync(a => a.PersonengruppeId == gruppeId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Gruppe bereits zugeteilt.");
        }

        db.PersonengruppeAgenten.Add(new PersonengruppeAgent
        {
            PersonengruppeId = gruppeId,
            AgentId = agentId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.PersonengruppeAgenten.FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken);
        if (zuteilung is null)
        {
            return;
        }
        db.PersonengruppeAgenten.Remove(zuteilung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PersonengruppeFortschritt> GetFortschrittAsync(string gruppeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Join auf Personen → der globale Soft-Delete-Filter zählt nur Mitglieder mit lebender Akte (x).
        var erfasst = await db.PersonengruppeMitglieder
            .Where(m => m.PersonengruppeId == gruppeId)
            .Join(db.Personen, m => m.PersonId, p => p.Id, (m, p) => m.Id)
            .CountAsync(cancellationToken);
        var geschaetzt = await db.Personengruppen
            .Where(g => g.Id == gruppeId)
            .Select(g => g.GeschaetzteMitgliederzahl)
            .FirstOrDefaultAsync(cancellationToken);
        return new PersonengruppeFortschritt(erfasst, geschaetzt);
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string gruppeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitgliedIds = await db.PersonengruppeMitglieder
            .Where(m => m.PersonengruppeId == gruppeId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var agentZuteilungIds = await db.PersonengruppeAgenten
            .Where(a => a.PersonengruppeId == gruppeId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(mitgliedIds) { gruppeId };
        ids.UnionWith(agentZuteilungIds);
        var typen = new[] { nameof(Personengruppe), nameof(PersonengruppeMitglied), nameof(PersonengruppeAgent) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Synchronisiert die automatischen „Gruppenkollege"-Verknüpfungen der Person (analog zu den
    /// Fraktionskollegen): zwischen P und Q soll genau dann eine bestehen, wenn beide mindestens eine
    /// Personengruppe teilen. Wird nach jeder Mitglieder-Änderung für die betroffene Person aufgerufen.
    /// </summary>
    private static async Task GruppenkollegenSyncAsync(AppDbContext db, string personId, CancellationToken cancellationToken)
    {
        var meineGruppen = await db.PersonengruppeMitglieder
            .Where(m => m.PersonId == personId)
            .Select(m => m.PersonengruppeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var soll = meineGruppen.Count == 0
            ? new List<string>()
            : await db.PersonengruppeMitglieder
                .Where(m => meineGruppen.Contains(m.PersonengruppeId) && m.PersonId != personId)
                .Select(m => m.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await KollegenSync.SyncAsync(db, personId, KollegenSync.Gruppenkollege, soll, cancellationToken);
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
