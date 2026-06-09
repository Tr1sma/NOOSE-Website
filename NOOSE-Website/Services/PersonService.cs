using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonService" />
public class PersonService(IDbContextFactory<AppDbContext> dbFactory, IFileStorageService fileStorage, ISteckbriefVorschlagService vorschlag, IAktenzeichenService aktenzeichen) : IPersonService
{
    public async Task<List<Person>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Personen
            .Where(p => istFuehrung || !p.IstVerschlusssache)
            .Include(p => p.Aliase)
            .OrderByDescending(p => p.GeaendertAm ?? p.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Person?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen
            .Include(p => p.Aliase)
            .Include(p => p.Telefonnummern)
            .Include(p => p.Fahrzeuge)
            .Include(p => p.Orte)
            .Include(p => p.Waffen)
            .Include(p => p.Fotos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (person is null || (person.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return person;
    }

    public async Task<List<Person>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Personen.IgnoreQueryFilters()
            .Where(p => p.IstGeloescht)
            .OrderByDescending(p => p.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Person>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Personen.Where(p => istFuehrung || !p.IstVerschlusssache);

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

    public async Task<List<Person>> FindeDuplikateAsync(string name, IEnumerable<string> telefonnummern, CancellationToken cancellationToken = default)
    {
        var nameLower = (name ?? string.Empty).Trim().ToLower();
        var nummern = telefonnummern
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Personen
            .Include(p => p.Telefonnummern)
            .Where(p => p.Name.ToLower() == nameLower
                     || p.Telefonnummern.Any(t => nummern.Contains(t.Nummer)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Person> ErstellenAsync(PersonEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(eingabe.Einstufung, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var person = new Person
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "P", cancellationToken),
            Name = eingabe.Name.Trim(),
            Beschreibung = Leer(eingabe.Beschreibung),
            Lebensstatus = eingabe.Lebensstatus,
            TotBis = eingabe.Lebensstatus == Lebensstatus.Tot ? LebensstatusLogic.TotBisAb(DateTime.UtcNow) : null,
            Einstufung = eingabe.Einstufung,
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };
        KinderMappen(person, eingabe);
        await VorschlaegeVormerkenAsync(db, person, cancellationToken);

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Person), person.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        db.Personen.Add(person);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return person;
    }

    public async Task AktualisierenAsync(string id, PersonEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen
            .Include(p => p.Aliase)
            .Include(p => p.Telefonnummern)
            .Include(p => p.Fahrzeuge)
            .Include(p => p.Orte)
            .Include(p => p.Waffen)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        var altStatus = person.Lebensstatus;
        var altTotBis = person.TotBis;

        person.Name = eingabe.Name.Trim();
        person.Beschreibung = Leer(eingabe.Beschreibung);
        person.IstVerschlusssache = eingabe.IstVerschlusssache;
        person.Lebensstatus = eingabe.Lebensstatus;
        if (eingabe.Lebensstatus == Lebensstatus.Tot)
        {
            // Manuell auf Tot: 20-Minuten-Fenster ab jetzt (sofern nicht bereits eines läuft).
            if (!LebensstatusLogic.IstTotFenster(altStatus, altTotBis, DateTime.UtcNow))
            {
                person.TotBis = LebensstatusLogic.TotBisAb(DateTime.UtcNow);
            }
        }
        else
        {
            person.TotBis = null;
        }

        // Steckbrief-Kinder vollständig ersetzen (alte hart löschen, neue anlegen).
        db.PersonAliase.RemoveRange(person.Aliase);
        db.PersonTelefone.RemoveRange(person.Telefonnummern);
        db.PersonFahrzeuge.RemoveRange(person.Fahrzeuge);
        db.PersonOrte.RemoveRange(person.Orte);
        db.PersonWaffen.RemoveRange(person.Waffen);
        KinderMappen(person, eingabe);
        await VorschlaegeVormerkenAsync(db, person, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");
        // Hard-Delete wird vom Interceptor in Soft-Delete umgewandelt (+ Audit „Geloescht").
        db.Personen.Remove(person);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        person.IstGeloescht = false;
        person.GeloeschtAm = null;
        person.GeloeschtVonId = null;
        // Interceptor erkennt den Übergang true → false und schreibt „Wiederhergestellt".
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        EinstufungHelfer.PruefeRangGate(neu, handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        person.Einstufung = neu;
        db.EinstufungVerlauf.Add(EinstufungHelfer.Eintrag(nameof(Person), id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == nameof(Person) && e.EntitaetId == id)
            .OrderByDescending(e => e.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonZugehoerigkeit>> GetZugehoerigkeitenAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Join auf die (soft-delete-gefilterten) Eltern-Akten → gelöschte Fraktionen/Gruppen fallen weg.
        // Der globale Soft-Delete-Filter blendet beendete Mitgliedschaften aus → nur aktive (BeendetAm = null).
        var fraktionen = await (
            from m in db.FraktionMitglieder
            where m.PersonId == personId
            join f in db.Fraktionen on m.FraktionId equals f.Id
            where istFuehrung || !f.IstVerschlusssache
            orderby f.Name
            select new PersonZugehoerigkeit(nameof(Fraktion), m.Id, f.Id, f.Name, f.Aktenzeichen, m.Rang, m.IstLeitung, m.ErstelltAm, (DateTime?)null))
            .ToListAsync(cancellationToken);

        var gruppen = await (
            from m in db.PersonengruppeMitglieder
            where m.PersonId == personId
            join g in db.Personengruppen on m.PersonengruppeId equals g.Id
            where istFuehrung || !g.IstVerschlusssache
            orderby g.Name
            select new PersonZugehoerigkeit(nameof(Personengruppe), m.Id, g.Id, g.Name, g.Aktenzeichen, m.Rolle, m.IstLeitung, m.ErstelltAm, (DateTime?)null))
            .ToListAsync(cancellationToken);

        return fraktionen.Concat(gruppen).ToList();
    }

    public async Task<List<PersonZugehoerigkeit>> GetEhemaligeZugehoerigkeitenAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // IgnoreQueryFilters: beendete (soft-gelöschte) Mitgliedschaften gezielt holen. Achtung – das schaltet
        // ALLE Filter der Query ab, auch die der Eltern-Akte → Papierkorb/Verschlusssache hier manuell nachsetzen.
        var fraktionen = await (
            from m in db.FraktionMitglieder.IgnoreQueryFilters()
            where m.PersonId == personId && m.IstGeloescht
            join f in db.Fraktionen on m.FraktionId equals f.Id
            where !f.IstGeloescht && (istFuehrung || !f.IstVerschlusssache)
            select new PersonZugehoerigkeit(nameof(Fraktion), m.Id, f.Id, f.Name, f.Aktenzeichen, m.Rang, m.IstLeitung, m.ErstelltAm, m.GeloeschtAm))
            .ToListAsync(cancellationToken);

        var gruppen = await (
            from m in db.PersonengruppeMitglieder.IgnoreQueryFilters()
            where m.PersonId == personId && m.IstGeloescht
            join g in db.Personengruppen on m.PersonengruppeId equals g.Id
            where !g.IstGeloescht && (istFuehrung || !g.IstVerschlusssache)
            select new PersonZugehoerigkeit(nameof(Personengruppe), m.Id, g.Id, g.Name, g.Aktenzeichen, m.Rolle, m.IstLeitung, m.ErstelltAm, m.GeloeschtAm))
            .ToListAsync(cancellationToken);

        // Neueste Beendigung zuerst (typübergreifend).
        return fraktionen.Concat(gruppen).OrderByDescending(z => z.BeendetAm).ToList();
    }

    public async Task<List<AbgeleiteteBeziehung>> GetAbgeleiteteBeziehungenAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 1. Eigene, für den Betrachter sichtbare Organisationen (Fraktionen + Gruppen).
        var meineFraktionen = await db.FraktionMitglieder
            .Where(m => m.PersonId == personId)
            .Join(db.Fraktionen, m => m.FraktionId, f => f.Id, (m, f) => f)
            .Where(f => istFuehrung || !f.IstVerschlusssache)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(cancellationToken);
        var meineGruppen = await db.PersonengruppeMitglieder
            .Where(m => m.PersonId == personId)
            .Join(db.Personengruppen, m => m.PersonengruppeId, g => g.Id, (m, g) => g)
            .Where(g => istFuehrung || !g.IstVerschlusssache)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        var orgNamen = new Dictionary<string, string>();
        foreach (var f in meineFraktionen) orgNamen[$"{nameof(Fraktion)}|{f.Id}"] = f.Name;
        foreach (var g in meineGruppen) orgNamen[$"{nameof(Personengruppe)}|{g.Id}"] = g.Name;
        if (orgNamen.Count == 0)
        {
            return new();
        }

        // 2. Bündnis-/Konflikt-Verknüpfungen, an denen eine meiner Organisationen beteiligt ist.
        var meineOrgIds = meineFraktionen.Select(f => f.Id).Concat(meineGruppen.Select(g => g.Id)).ToList();
        var roh = await db.Verknuepfungen
            .Where(v => (v.Art == VerknuepfungArt.Buendnis || v.Art == VerknuepfungArt.Konflikt)
                     && (meineOrgIds.Contains(v.VonId) || meineOrgIds.Contains(v.NachId)))
            .Select(v => new { v.VonTyp, v.VonId, v.NachTyp, v.NachId, v.Art })
            .ToListAsync(cancellationToken);

        // Je Verknüpfung die eigene Seite (Quelle) und die Partner-Organisation bestimmen.
        var partner = new List<(string QuelleKey, string PartnerTyp, string PartnerId, VerknuepfungArt Art)>();
        foreach (var v in roh)
        {
            var vonKey = $"{v.VonTyp}|{v.VonId}";
            var nachKey = $"{v.NachTyp}|{v.NachId}";
            if (orgNamen.ContainsKey(vonKey))
            {
                partner.Add((vonKey, v.NachTyp, v.NachId, v.Art));
            }
            else if (orgNamen.ContainsKey(nachKey))
            {
                partner.Add((nachKey, v.VonTyp, v.VonId, v.Art));
            }
        }
        if (partner.Count == 0)
        {
            return new();
        }

        // 3. Sichtbare Partner-Organisationen auflösen (Name; Verschlusssache nur für Führung).
        var partnerFraktionIds = partner.Where(p => p.PartnerTyp == nameof(Fraktion)).Select(p => p.PartnerId).Distinct().ToList();
        var partnerGruppenIds = partner.Where(p => p.PartnerTyp == nameof(Personengruppe)).Select(p => p.PartnerId).Distinct().ToList();
        var partnerFraktionen = (await db.Fraktionen
            .Where(f => partnerFraktionIds.Contains(f.Id) && (istFuehrung || !f.IstVerschlusssache))
            .Select(f => new { f.Id, f.Name }).ToListAsync(cancellationToken)).ToDictionary(f => f.Id, f => f.Name);
        var partnerGruppen = (await db.Personengruppen
            .Where(g => partnerGruppenIds.Contains(g.Id) && (istFuehrung || !g.IstVerschlusssache))
            .Select(g => new { g.Id, g.Name }).ToListAsync(cancellationToken)).ToDictionary(g => g.Id, g => g.Name);

        // 4. Mitglieder der Partner-Organisationen (Person-Ids je Partner).
        var sichtbareFraktionIds = partnerFraktionen.Keys.ToList();
        var sichtbareGruppenIds = partnerGruppen.Keys.ToList();
        var fraktionMitglieder = await db.FraktionMitglieder
            .Where(m => sichtbareFraktionIds.Contains(m.FraktionId))
            .Select(m => new { m.FraktionId, m.PersonId }).ToListAsync(cancellationToken);
        var gruppenMitglieder = await db.PersonengruppeMitglieder
            .Where(m => sichtbareGruppenIds.Contains(m.PersonengruppeId))
            .Select(m => new { m.PersonengruppeId, m.PersonId }).ToListAsync(cancellationToken);
        var mitgliederJePartner = fraktionMitglieder.GroupBy(m => m.FraktionId).ToDictionary(g => g.Key, g => g.Select(x => x.PersonId).ToList());
        foreach (var grp in gruppenMitglieder.GroupBy(m => m.PersonengruppeId))
        {
            mitgliederJePartner[grp.Key] = grp.Select(x => x.PersonId).ToList();
        }

        // 5. Kandidaten bilden (dedupliziert je (Person, Art); sich selbst ausschließen).
        var kandidaten = new Dictionary<(string PersonId, VerknuepfungArt Art), (string QuelleName, string PartnerName)>();
        foreach (var p in partner)
        {
            var partnerName = p.PartnerTyp == nameof(Fraktion)
                ? (partnerFraktionen.TryGetValue(p.PartnerId, out var fn) ? fn : null)
                : (partnerGruppen.TryGetValue(p.PartnerId, out var gn) ? gn : null);
            if (partnerName is null || !mitgliederJePartner.TryGetValue(p.PartnerId, out var mids))
            {
                continue;
            }
            var quelleName = orgNamen[p.QuelleKey];
            foreach (var mid in mids)
            {
                if (mid == personId)
                {
                    continue;
                }
                kandidaten.TryAdd((mid, p.Art), (quelleName, partnerName));
            }
        }
        if (kandidaten.Count == 0)
        {
            return new();
        }

        // 6. Personen auflösen (Name/Aktenzeichen; Verschlusssache nur für Führung).
        var personIds = kandidaten.Keys.Select(k => k.PersonId).Distinct().ToList();
        var personen = (await db.Personen
            .Where(p => personIds.Contains(p.Id) && (istFuehrung || !p.IstVerschlusssache))
            .Select(p => new { p.Id, p.Name, p.Aktenzeichen }).ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id);

        var ergebnis = new List<AbgeleiteteBeziehung>();
        foreach (var ((pid, art), (quelleName, partnerName)) in kandidaten)
        {
            if (!personen.TryGetValue(pid, out var person))
            {
                continue;
            }
            ergebnis.Add(new AbgeleiteteBeziehung(art, person.Id, person.Name, person.Aktenzeichen, quelleName, partnerName));
        }
        return ergebnis
            .OrderBy(e => e.Art)
            .ThenBy(e => e.PersonName)
            .ToList();
    }

    public async Task<PersonFoto> FotoHinzufuegenAsync(string personId, Stream inhalt, string originalName, string contentType, long groesse, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (!fileStorage.IstErlaubterTyp(contentType))
        {
            throw new InvalidOperationException($"Dateityp '{contentType}' ist nicht erlaubt.");
        }

        var dateiname = await fileStorage.SpeichernAsync(inhalt, contentType, cancellationToken);
        var foto = new PersonFoto
        {
            PersonId = personId,
            DateinameGespeichert = dateiname,
            OriginalName = originalName,
            ContentType = contentType,
            GroesseBytes = groesse,
            ErstelltAm = DateTime.UtcNow,
            ErstelltVonId = handelnder.GetAgentId(),
        };
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.PersonFotos.Add(foto);
        await db.SaveChangesAsync(cancellationToken);
        return foto;
    }

    public async Task FotoEntfernenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var foto = await db.PersonFotos.FirstOrDefaultAsync(f => f.Id == fotoId, cancellationToken);
        if (foto is null)
        {
            return;
        }
        // Erst den DB-Datensatz entfernen (Quelle der Wahrheit), dann die Datei löschen. So bleibt
        // bei einem Speicherfehler kein verwaister Datensatz zurück, der auf eine fehlende Datei zeigt.
        db.PersonFotos.Remove(foto);
        await db.SaveChangesAsync(cancellationToken);
        fileStorage.Loeschen(foto.DateinameGespeichert);
    }

    public async Task<PersonFoto?> GetFotoMitPersonAsync(string fotoId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonFotos.Include(f => f.Person).FirstOrDefaultAsync(f => f.Id == fotoId, cancellationToken);
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string personId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Kind-IDs (Doks) einsammeln, damit deren Audit-Einträge in der Akten-Historie erscheinen.
        var dokIds = await db.PersonDoks.IgnoreQueryFilters()
            .Where(d => d.PersonId == personId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(dokIds) { personId };
        var typen = new[] { nameof(Person), nameof(PersonDok) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    // ---- Helfer ----

    private static void KinderMappen(Person person, PersonEingabe eingabe)
    {
        person.Aliase = eingabe.Aliase
            .Where(a => !string.IsNullOrWhiteSpace(a.Aliasname))
            .Select(a => new PersonAlias { PersonId = person.Id, Aliasname = a.Aliasname.Trim() })
            .ToList();
        person.Telefonnummern = eingabe.Telefonnummern
            .Where(t => !string.IsNullOrWhiteSpace(t.Nummer))
            .Select(t => new PersonTelefon { PersonId = person.Id, Nummer = t.Nummer.Trim(), Bezeichnung = Leer(t.Bezeichnung) })
            .ToList();
        person.Fahrzeuge = eingabe.Fahrzeuge
            .Where(f => !string.IsNullOrWhiteSpace(f.Bezeichnung) || !string.IsNullOrWhiteSpace(f.Kennzeichen))
            .Select(f => new PersonFahrzeug { PersonId = person.Id, Bezeichnung = (f.Bezeichnung ?? string.Empty).Trim(), Kennzeichen = Leer(f.Kennzeichen) })
            .ToList();
        person.Orte = eingabe.Orte
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .Select(o => new PersonOrt { PersonId = person.Id, Text = o.Text.Trim(), Notiz = Leer(o.Notiz) })
            .ToList();
        person.Waffen = eingabe.Waffen
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => new PersonWaffe { PersonId = person.Id, Text = w.Text.Trim() })
            .ToList();
    }

    /// <summary>
    /// Speist die erfassten Steckbrief-Werte in den gemeinsamen Vorschlagskatalog ein (Waffen/Fahrzeuge/Orte).
    /// Verschlusssachen bleiben außen vor, damit klassifizierte Werte nicht in die geteilte Liste gelangen.
    /// Merkt nur im übergebenen Context vor – persistiert wird mit dem nachfolgenden SaveChanges der Person (atomar).
    /// </summary>
    private async Task VorschlaegeVormerkenAsync(AppDbContext db, Person person, CancellationToken cancellationToken)
    {
        if (person.IstVerschlusssache)
        {
            return;
        }
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Waffe, person.Waffen.Select(w => w.Text), cancellationToken);
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Fahrzeug, person.Fahrzeuge.Select(f => f.Bezeichnung), cancellationToken);
        await vorschlag.VormerkenAsync(db, VorschlagTyp.Ort, person.Orte.Select(o => o.Text), cancellationToken);
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
