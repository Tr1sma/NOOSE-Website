using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Fraktionen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IFraktionService" />
public class FraktionService(IDbContextFactory<AppDbContext> dbFactory, IAktenzeichenService aktenzeichen, ISteckbriefVorschlagService vorschlag, IPersonService personService) : IFraktionService
{
    public async Task<List<Fraktion>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Fraktionen
            .Where(f => istFuehrung || !f.IstVerschlusssache)
            .Include(f => f.Mitglieder)
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
        };
        KinderMappen(fraktion, eingabe);
        await VorschlaegeVormerkenAsync(db, fraktion, cancellationToken);

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Fraktion), fraktion.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        db.Fraktionen.Add(fraktion);
        await db.SaveChangesAsync(cancellationToken);
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

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

        // Strukturierte Listen vollständig ersetzen (Mitglieder bleiben unangetastet – eigene Endpunkte).
        db.FraktionRaenge.RemoveRange(fraktion.Raenge);
        db.FraktionWaffenbestaende.RemoveRange(fraktion.Waffenbestand);
        db.FraktionLagerbestaende.RemoveRange(fraktion.Lagerbestand);
        KinderMappen(fraktion, eingabe);
        await VorschlaegeVormerkenAsync(db, fraktion, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fraktion = await db.Fraktionen.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");
        // Hard-Delete → Interceptor wandelt in Soft-Delete um (+ Audit „Geloescht").
        db.Fraktionen.Remove(fraktion);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
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

        fraktion.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Fraktion), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
        if (!await db.Fraktionen.AnyAsync(f => f.Id == fraktionId, cancellationToken))
        {
            throw new InvalidOperationException($"Fraktion '{fraktionId}' nicht gefunden.");
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

    public async Task MitgliedAendernAsync(string mitgliedId, string? rang, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.FraktionMitglieder.FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        mitglied.Rang = Leer(rang);
        mitglied.IstLeitung = istLeitung;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitglied = await db.FraktionMitglieder.FirstOrDefaultAsync(m => m.Id == mitgliedId, cancellationToken);
        if (mitglied is null)
        {
            return;
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

    public async Task<List<AuditLog>> GetHistorieAsync(string fraktionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mitgliedIds = await db.FraktionMitglieder
            .Where(m => m.FraktionId == fraktionId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(mitgliedIds) { fraktionId };
        var typen = new[] { nameof(Fraktion), nameof(FraktionMitglied) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
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

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
