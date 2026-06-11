using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Fraktionen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IFraktionService" />
public class FraktionService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen, ISteckbriefVorschlagService vorschlag, IPersonService personService, IFraktionFotoStorageService fotoStorage) : IFraktionService
{
    public async Task<List<Fraktion>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Mitglieder inkl. Person laden, damit die Listen-Mitgliederzahl exakt der Detailansicht entspricht
        // (gelöschte/Verschlusssache-Personen werden dort wie hier ausgeblendet).
        return await db.Fraktionen
            .Where(f => istFuehrung || !f.IstVerschlusssache)
            .Include(f => f.Mitglieder).ThenInclude(m => m.Person)
            .Include(f => f.Fotos)
            .OrderByDescending(f => f.GeaendertAm ?? f.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Fraktion?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen
            .Include(f => f.Raenge)
            .Include(f => f.Waffenbestand)
            .Include(f => f.Lagerbestand)
            .Include(f => f.Drogenrouten)
            .Include(f => f.Fotos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (fraktion is null || (fraktion.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return fraktion;
    }

    public async Task<List<Fraktion>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Fraktionen.IgnoreQueryFilters()
            .Where(f => f.IstGeloescht)
            .OrderByDescending(f => f.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Fraktion>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Fraktionen.Where(f => istFuehrung || !f.IstVerschlusssache);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(f => f.Name.Contains(s) || f.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(f => f.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Fraktion> ErstellenAsync(FraktionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(eingabe.Einstufung, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var fraktion = new Fraktion
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "F", cancellationToken),
            Name = eingabe.Name.Trim(),
            Art = Leer(eingabe.Art),
            Funk = Leer(eingabe.Funk),
            Darkchat = Leer(eingabe.Darkchat),
            Ausstellungszeiten = Leer(eingabe.Ausstellungszeiten),
            Anwesen = Leer(eingabe.Anwesen),
            Erkennungsfarbe = Leer(eingabe.Erkennungsfarbe),
            Ziele = Leer(eingabe.Ziele),
            Beschreibung = Leer(eingabe.Beschreibung),
            Einstufung = eingabe.Einstufung,
            IstVerschlusssache = eingabe.IstVerschlusssache,
            IstStaatsfraktion = eingabe.IstStaatsfraktion,
            GeschaetzteMitgliederzahl = eingabe.GeschaetzteMitgliederzahl,
        };
        KinderMappen(fraktion, eingabe);
        await VorschlaegeVormerkenAsync(db, fraktion, cancellationToken);

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Fraktion), fraktion.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        db.Fraktionen.Add(fraktion);
        await db.SaveChangesAsync(cancellationToken);

        // Im Anlege-Formular erfasste Mitglieder übernehmen (bestehende Personen + automatisch angelegte neue
        // Akten, dedupliziert) und anschließend die Fraktionskollegen-Verknüpfungen aufbauen – analog zur Gruppe.
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
                db.FraktionMitglieder.Add(new FraktionMitglied
                {
                    FraktionId = fraktion.Id,
                    PersonId = pid,
                    Rang = Leer(m.Rang),
                    IstLeitung = m.IstLeitung,
                });
                hinzugefuegt.Add(pid);
            }
            if (hinzugefuegt.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                foreach (var pid in hinzugefuegt)
                {
                    await FraktionskollegenSyncAsync(db, pid, cancellationToken);
                }
            }
        }

        // Ersteller automatisch zuteilen und als Ermittlungsleiter markieren (so existiert stets mindestens ein EL).
        var erstellerId = handelnder.GetAgentId();
        if (erstellerId is not null)
        {
            db.FraktionAgenten.Add(new FraktionAgent
            {
                FraktionId = fraktion.Id,
                AgentId = erstellerId,
                IstErmittlungsleiter = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return fraktion;
    }

    public async Task AktualisierenAsync(string id, FraktionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen
            .Include(f => f.Raenge)
            .Include(f => f.Waffenbestand)
            .Include(f => f.Lagerbestand)
            .Include(f => f.Drogenrouten)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

        if (fraktion.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        fraktion.Name = eingabe.Name.Trim();
        fraktion.Art = Leer(eingabe.Art);
        fraktion.Funk = Leer(eingabe.Funk);
        fraktion.Darkchat = Leer(eingabe.Darkchat);
        fraktion.Ausstellungszeiten = Leer(eingabe.Ausstellungszeiten);
        fraktion.Anwesen = Leer(eingabe.Anwesen);
        fraktion.Erkennungsfarbe = Leer(eingabe.Erkennungsfarbe);
        fraktion.Ziele = Leer(eingabe.Ziele);
        fraktion.Beschreibung = Leer(eingabe.Beschreibung);
        fraktion.IstVerschlusssache = eingabe.IstVerschlusssache;
        fraktion.IstStaatsfraktion = eingabe.IstStaatsfraktion;
        fraktion.GeschaetzteMitgliederzahl = eingabe.GeschaetzteMitgliederzahl;

        // Strukturierte Listen vollständig ersetzen (Mitglieder bleiben unangetastet – eigene Endpunkte).
        db.FraktionRaenge.RemoveRange(fraktion.Raenge);
        db.FraktionWaffenbestaende.RemoveRange(fraktion.Waffenbestand);
        db.FraktionLagerbestaende.RemoveRange(fraktion.Lagerbestand);
        db.FraktionDrogenrouten.RemoveRange(fraktion.Drogenrouten);
        KinderMappen(fraktion, eingabe);
        await VorschlaegeVormerkenAsync(db, fraktion, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");
        // Hard-Delete → Interceptor wandelt in Soft-Delete um (+ Audit „Geloescht").
        db.Fraktionen.Remove(fraktion);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

        fraktion.IstGeloescht = false;
        fraktion.GeloeschtAm = null;
        fraktion.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(neu, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

        if (fraktion.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        fraktion.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Fraktion), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Fraktion), id, istFuehrung, cancellationToken))
        {
            return new();
        }
        return await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == nameof(Fraktion) && e.EntitaetId == id)
            .OrderByDescending(e => e.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FraktionMitglied>> GetMitgliederAsync(string fraktionId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglieder = await db.FraktionMitglieder
            .Where(m => m.FraktionId == fraktionId)
            .Include(m => m.Person)
            .ToListAsync(cancellationToken);

        // Person == null → Akte im Papierkorb (Soft-Delete-Filter greift auf die Navigation); ausblenden.
        // Verschlusssachen-Personen nur für Führung sichtbar.
        return mitglieder
            .Where(m => m.Person is not null && (istFuehrung || !m.Person.IstVerschlusssache))
            .OrderByDescending(m => m.IstLeitung)
            .ThenBy(m => m.Person!.Name)
            .ToList();
    }

    public async Task MitgliedHinzufuegenAsync(string fraktionId, MitgliedEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == fraktionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{fraktionId}' nicht gefunden.");
        if (fraktion.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var personId = await PersonIdErmittelnAsync(db, eingabe.PersonId, eingabe.NeuePersonName, handelnder, cancellationToken);
        if (await db.FraktionMitglieder.AnyAsync(m => m.FraktionId == fraktionId && m.PersonId == personId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Person ist bereits Mitglied der Fraktion.");
        }

        // Mitgliedschaft + automatische Fraktionskollegen-Verknüpfungen in EINER Transaktion,
        // damit nach außen kein Zwischenzustand (Mitglied ohne Kollegen-Links) sichtbar wird.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.FraktionMitglieder.Add(new FraktionMitglied
        {
            FraktionId = fraktionId,
            PersonId = personId,
            Rang = Leer(eingabe.Rang),
            IstLeitung = eingabe.IstLeitung,
        });
        await db.SaveChangesAsync(cancellationToken);
        await FraktionskollegenSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Liefert die Personen-Id: entweder die übergebene bestehende (mit Existenzprüfung) oder – wenn nur ein
    /// neuer Name angegeben ist – eine frisch angelegte Personen-Akte (committet, eigenes Aktenzeichen).
    /// </summary>
    private Task<string> PersonIdErmittelnAsync(AppDbContext db, string? personId, string? neuerName, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
        => MitgliedHelfer.PersonIdErmittelnAsync(db, personService, personId, neuerName, handelnder, cancellationToken);

    public async Task MitgliedAendernAsync(string mitgliedId, string? rang, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.FraktionMitglieder.Include(m => m.Fraktion).FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        if (mitglied.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        mitglied.Rang = Leer(rang);
        mitglied.IstLeitung = istLeitung;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.FraktionMitglieder.Include(m => m.Fraktion).FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken);
        if (mitglied is null)
        {
            return;
        }
        if (mitglied.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        var personId = mitglied.PersonId;
        // Austritt + Kollegen-Verknüpfungen nachführen in EINER Transaktion (kein Zwischenzustand).
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        // Soft-Delete (Join-Entity ist ISoftDelete): der Interceptor setzt GeloeschtAm (= Austrittsdatum) statt
        // hart zu löschen → die Mitgliedschaft bleibt als Verlaufseintrag erhalten. KollegenSync sieht danach
        // nur noch aktive Mitglieder (globaler Filter) und entfernt die Fraktionskollegen-Links korrekt.
        db.FraktionMitglieder.Remove(mitglied);
        await db.SaveChangesAsync(cancellationToken);
        await FraktionskollegenSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<List<FraktionAgent>> GetAgentenAsync(string fraktionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FraktionAgenten
            .Where(a => a.FraktionId == fraktionId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IstErmittlungsleiter)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FraktionAgent>> GetErmittlungsleiterAsync(string fraktionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FraktionAgenten
            .Where(a => a.FraktionId == fraktionId && a.IstErmittlungsleiter)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuteilenAsync(string fraktionId, string agentId, bool alsErmittlungsleiter, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == fraktionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{fraktionId}' nicht gefunden.");
        if (fraktion.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderELAsync(db, fraktionId, handelnder, cancellationToken);
        // Das Ermittlungsleiter-Flag darf nur die Führung vergeben (auch beim Zuteilen).
        if (alsErmittlungsleiter)
        {
            Berechtigung.VerlangeFuehrung(handelnder);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.FraktionAgenten.AnyAsync(a => a.FraktionId == fraktionId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Fraktion bereits zugeteilt.");
        }

        db.FraktionAgenten.Add(new FraktionAgent
        {
            FraktionId = fraktionId,
            AgentId = agentId,
            IstErmittlungsleiter = alsErmittlungsleiter,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.FraktionAgenten.Include(a => a.Fraktion).FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken);
        if (zuteilung is null)
        {
            return;
        }
        if (zuteilung.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await VerlangeFuehrungOderELAsync(db, zuteilung.FraktionId, handelnder, cancellationToken);
        db.FraktionAgenten.Remove(zuteilung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ErmittlungsleiterSetzenAsync(string zuteilungId, bool ist, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Ermittlungsleiter vergeben/entziehen ist der Führung vorbehalten.
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuteilung = await db.FraktionAgenten.FirstOrDefaultAsync(a => a.Id == zuteilungId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        zuteilung.IstErmittlungsleiter = ist;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Wirft, wenn der Handelnde weder Führung noch Ermittlungsleiter dieser Fraktion ist.</summary>
    private static async Task VerlangeFuehrungOderELAsync(AppDbContext db, string fraktionId, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var agentId = handelnder.GetAgentId();
        var istEL = agentId is not null && await db.FraktionAgenten
            .AnyAsync(a => a.FraktionId == fraktionId && a.AgentId == agentId && a.IstErmittlungsleiter, cancellationToken);
        if (!istEL)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Ermittlungsleiter dieser Akte.");
        }
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string fraktionId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Fraktion), fraktionId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var mitgliedIds = await db.FraktionMitglieder
            .Where(m => m.FraktionId == fraktionId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var agentZuteilungIds = await db.FraktionAgenten
            .Where(a => a.FraktionId == fraktionId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Beziehungen (Konflikte/Bündnisse), die diese Fraktion als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var beziehungIds = await db.Verknuepfungen
            .IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == nameof(Fraktion) && v.VonId == fraktionId)
                 || (v.NachTyp == nameof(Fraktion) && v.NachId == fraktionId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(mitgliedIds) { fraktionId };
        ids.UnionWith(agentZuteilungIds);
        ids.UnionWith(beziehungIds);
        var typen = new[] { nameof(Fraktion), nameof(FraktionMitglied), nameof(FraktionAgent), nameof(Verknuepfung) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    // ---- Aktivitäten (Zeitstrahl) ----

    public async Task<List<FraktionAktivitaet>> GetAktivitaetenAsync(string fraktionId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Defense in depth: an Aktivitäten einer Verschlusssache-Fraktion kommt nur die Führung.
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Fraktion), fraktionId, istFuehrung, cancellationToken))
        {
            return new();
        }
        return await db.FraktionAktivitaeten
            .Where(a => a.FraktionId == fraktionId)
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task AktivitaetHinzufuegenAsync(string fraktionId, AktivitaetEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var titel = eingabe.Titel?.Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == fraktionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{fraktionId}' nicht gefunden.");
        if (fraktion.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        db.FraktionAktivitaeten.Add(new FraktionAktivitaet
        {
            FraktionId = fraktionId,
            Titel = titel,
            Art = Leer(eingabe.Art),
            // Vom Nutzer gewählter Zeitpunkt (lokal erfasst) → als UTC speichern (App-Konvention, vgl. Wiedervorlagen).
            Zeitpunkt = eingabe.Zeitpunkt.ToUniversalTime(),
            Beschreibung = Leer(eingabe.Beschreibung),
            Ort = Leer(eingabe.Ort),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AktivitaetAendernAsync(string aktivitaetId, AktivitaetEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var titel = eingabe.Titel?.Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aktivitaet = await db.FraktionAktivitaeten.Include(a => a.Fraktion).FirstOrDefaultAsync(a => a.Id == aktivitaetId, cancellationToken)
            ?? throw new InvalidOperationException("Aktivität nicht gefunden.");
        if (aktivitaet.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        aktivitaet.Titel = titel;
        aktivitaet.Art = Leer(eingabe.Art);
        aktivitaet.Zeitpunkt = eingabe.Zeitpunkt.ToUniversalTime();
        aktivitaet.Beschreibung = Leer(eingabe.Beschreibung);
        aktivitaet.Ort = Leer(eingabe.Ort);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AktivitaetEntfernenAsync(string aktivitaetId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aktivitaet = await db.FraktionAktivitaeten.Include(a => a.Fraktion).FirstOrDefaultAsync(a => a.Id == aktivitaetId, cancellationToken);
        if (aktivitaet is null)
        {
            return;
        }
        if (aktivitaet.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        // Soft-Delete via Interceptor (bleibt als Verlaufseintrag im Papierkorb).
        db.FraktionAktivitaeten.Remove(aktivitaet);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<string>> GetAktivitaetArtenAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Distinct über alle (nicht gelöschten – globaler Filter) Aktivitäten, damit gängige Arten überall als Vorschlag auftauchen.
        return await db.FraktionAktivitaeten
            .Where(a => a.Art != null && a.Art != "")
            .Select(a => a.Art!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    // ---- Fotos (Galerie + Titelbild) ----

    public async Task<List<FraktionFoto>> GetFotosAsync(string fraktionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Titelbild zuerst, danach nach Aufnahmezeitpunkt.
        return await db.FraktionFotos
            .Where(f => f.FraktionId == fraktionId)
            .OrderByDescending(f => f.IstTitelbild)
            .ThenBy(f => f.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<FraktionFoto?> GetFotoMitFraktionAsync(string fotoId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FraktionFotos.Include(f => f.Fraktion).FirstOrDefaultAsync(f => f.Id == fotoId, cancellationToken);
    }

    public async Task<FraktionFoto> FotoHinzufuegenAsync(string fraktionId, Stream inhalt, string originalName, string contentType, long groesse, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (!fotoStorage.IstErlaubterTyp(contentType))
        {
            throw new InvalidOperationException($"Dateityp '{contentType}' ist nicht erlaubt.");
        }
        // Größenlimit serverseitig erzwingen (nicht nur in der UI) – verhindert Disk-Filling über andere Pfade.
        if (groesse > fotoStorage.MaxBytes)
        {
            throw new InvalidOperationException($"Datei zu groß (max. {fotoStorage.MaxBytes / (1024 * 1024)} MB).");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Existenz + Verschlusssache-Sichtbarkeit der Akte prüfen, BEVOR eine Datei geschrieben wird.
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == fraktionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{fraktionId}' nicht gefunden.");
        if (fraktion.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Das erste Foto der Fraktion wird automatisch Titelbild (Steckkarte zeigt sofort ein Bild).
        var istErstes = !await db.FraktionFotos.AnyAsync(f => f.FraktionId == fraktionId, cancellationToken);

        var dateiname = await fotoStorage.SpeichernAsync(inhalt, contentType, cancellationToken);
        var foto = new FraktionFoto
        {
            FraktionId = fraktionId,
            DateinameGespeichert = dateiname,
            OriginalName = originalName,
            ContentType = contentType,
            GroesseBytes = groesse,
            IstTitelbild = istErstes,
            ErstelltAm = DateTime.UtcNow,
            ErstelltVonId = handelnder.GetAgentId(),
        };
        db.FraktionFotos.Add(foto);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Schlägt der DB-Insert fehl, die bereits geschriebene Datei wieder entfernen (kein verwaister Anhang).
            fotoStorage.Loeschen(dateiname);
            throw;
        }
        return foto;
    }

    public async Task FotoEntfernenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var foto = await db.FraktionFotos.Include(f => f.Fraktion).FirstOrDefaultAsync(f => f.Id == fotoId, cancellationToken);
        if (foto is null)
        {
            return;
        }
        if (foto.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        // Erst den DB-Datensatz entfernen (Quelle der Wahrheit), dann die Datei löschen. So bleibt
        // bei einem Speicherfehler kein verwaister Datensatz zurück, der auf eine fehlende Datei zeigt.
        db.FraktionFotos.Remove(foto);
        await db.SaveChangesAsync(cancellationToken);
        fotoStorage.Loeschen(foto.DateinameGespeichert);
    }

    public async Task AlsTitelbildSetzenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var foto = await db.FraktionFotos.Include(f => f.Fraktion).FirstOrDefaultAsync(f => f.Id == fotoId, cancellationToken)
            ?? throw new InvalidOperationException($"Foto '{fotoId}' nicht gefunden.");
        if (foto.Fraktion?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Genau ein Titelbild je Fraktion: alle Geschwister-Fotos zurücksetzen, dieses markieren (eine SaveChanges = atomar).
        var geschwister = await db.FraktionFotos.Where(f => f.FraktionId == foto.FraktionId).ToListAsync(cancellationToken);
        foreach (var g in geschwister)
        {
            g.IstTitelbild = g.Id == fotoId;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- Helfer ----

    private static void KinderMappen(Fraktion fraktion, FraktionEingabe eingabe)
    {
        fraktion.Raenge = eingabe.Raenge
            .Where(r => !string.IsNullOrWhiteSpace(r.Bezeichnung))
            .Select((r, i) => new FraktionRang { FraktionId = fraktion.Id, Bezeichnung = r.Bezeichnung.Trim(), Reihenfolge = i })
            .ToList();
        fraktion.Waffenbestand = eingabe.Waffenbestand
            .Where(w => !string.IsNullOrWhiteSpace(w.Bezeichnung))
            .Select(w => new FraktionWaffenbestand { FraktionId = fraktion.Id, Bezeichnung = w.Bezeichnung.Trim(), Menge = Leer(w.Menge) })
            .ToList();
        fraktion.Lagerbestand = eingabe.Lagerbestand
            .Where(l => !string.IsNullOrWhiteSpace(l.Bezeichnung))
            .Select(l => new FraktionLagerbestand { FraktionId = fraktion.Id, Bezeichnung = l.Bezeichnung.Trim(), Menge = Leer(l.Menge) })
            .ToList();
        // Drogenrouten teilen das generische BestandEingabe; dessen Menge-/Zusatzfeld trägt hier die Notiz.
        fraktion.Drogenrouten = eingabe.Drogenrouten
            .Where(d => !string.IsNullOrWhiteSpace(d.Bezeichnung))
            .Select(d => new FraktionDrogenroute { FraktionId = fraktion.Id, Bezeichnung = d.Bezeichnung.Trim(), Notiz = Leer(d.Menge) })
            .ToList();
    }

    /// <summary>
    /// Speist Bestands-Bezeichnungen in den gemeinsamen Vorschlagskatalog ein (Waffen → Waffe, Lager → Lagerbestand).
    /// Verschlusssachen bleiben außen vor. Merkt nur im übergebenen Context vor – persistiert wird mit SaveChanges.
    /// </summary>
    private async Task VorschlaegeVormerkenAsync(AppDbContext db, Fraktion fraktion, CancellationToken cancellationToken)
    {
        if (fraktion.IstVerschlusssache)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(fraktion.Art))
        {
            await vorschlag.VormerkenAsync(db, VorschlagTyp.Art, new[] { fraktion.Art }, cancellationToken);
        }
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Waffe, fraktion.Waffenbestand.Select(w => w.Bezeichnung), cancellationToken);
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Lagerbestand, fraktion.Lagerbestand.Select(l => l.Bezeichnung), cancellationToken);
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Drogenroute, fraktion.Drogenrouten.Select(d => d.Bezeichnung), cancellationToken);
    }

    /// <summary>
    /// Synchronisiert die automatischen „Fraktionskollege"-Verknüpfungen der Person: Zwischen P und Q soll
    /// genau dann eine automatische Verknüpfung bestehen, wenn beide mindestens eine Fraktion teilen. Wird
    /// nach jeder Mitglieder-Änderung für die betroffene Person aufgerufen (greift auch von der Fraktionsseite).
    /// </summary>
    /// <remarks>
    /// Invariante: pro Personen-Paar genau EINE automatische Verknüpfung (eine Richtung). Es genügt, nur die
    /// betroffene Person P abzugleichen, weil eine Verknüpfung nur eine Zeile ist und hier beide Richtungen
    /// (<c>VonId == P || NachId == P</c>) berücksichtigt werden – ein späterer Abgleich von Q findet die
    /// Zeile von P aus wieder und legt keine Gegen-Richtung an. Etwaige Alt-Duplikate werden hier mit
    /// abgeräumt. Bewusst KEIN Unique-Index auf (Von/Nach), da der für manuelle, soft-gelöschte
    /// Verknüpfungen kollidieren würde. Automatische Verknüpfungen werden hart gelöscht (maschinell gepflegt,
    /// kein Papierkorb, keine Soft-Delete-Reste).
    /// </remarks>
    private static async Task FraktionskollegenSyncAsync(AppDbContext db, string personId, CancellationToken cancellationToken)
    {
        var meineFraktionen = await db.FraktionMitglieder
            .Where(m => m.PersonId == personId)
            .Select(m => m.FraktionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var soll = meineFraktionen.Count == 0
            ? new List<string>()
            : await db.FraktionMitglieder
                .Where(m => meineFraktionen.Contains(m.FraktionId) && m.PersonId != personId)
                .Select(m => m.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await KollegenSync.SyncAsync(db, personId, KollegenSync.Fraktionskollege, soll, cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
