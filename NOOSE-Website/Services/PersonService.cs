using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonService" />
public class PersonService(AppDbContext db, IFileStorageService fileStorage) : IPersonService
{
    public Task<List<Person>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
        => db.Personen
            .Where(p => istFuehrung || !p.IstVerschlusssache)
            .Include(p => p.Aliase)
            .OrderByDescending(p => p.GeaendertAm ?? p.ErstelltAm)
            .ToListAsync(cancellationToken);

    public async Task<Person?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        var person = await db.Personen
            .Include(p => p.Aliase)
            .Include(p => p.Telefonnummern)
            .Include(p => p.Fahrzeuge)
            .Include(p => p.Orte)
            .Include(p => p.Waffen)
            .Include(p => p.Fotos)
            .Include(p => p.EinstufungVerlauf)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (person is null || (person.IstVerschlusssache && !istFuehrung))
        {
            return null;
        }
        return person;
    }

    public Task<List<Person>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
        => db.Personen.IgnoreQueryFilters()
            .Where(p => p.IstGeloescht)
            .OrderByDescending(p => p.GeloeschtAm)
            .ToListAsync(cancellationToken);

    public async Task<List<Person>> FindeDuplikateAsync(string name, IEnumerable<string> telefonnummern, CancellationToken cancellationToken = default)
    {
        var nameLower = (name ?? string.Empty).Trim().ToLower();
        var nummern = telefonnummern
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();

        return await db.Personen
            .Include(p => p.Telefonnummern)
            .Where(p => p.Name.ToLower() == nameLower
                     || p.Telefonnummern.Any(t => nummern.Contains(t.Nummer)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Person> ErstellenAsync(PersonEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (eingabe.Einstufung == Einstufung.GesichertStaatsgefaehrdend && !handelnder.DarfHoechsteEinstufung())
        {
            throw new InvalidOperationException(
                "'Gesichert staatsgefährdend' darf erst ab Senior Special Agent direkt gesetzt werden – sonst per Antrag.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var jahr = DateTime.UtcNow.Year;
        // Atomarer, race-sicherer Zähler-Inkrement (MariaDB/MySQL-nativ).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO AktenzeichenZaehler (Jahr, LetzteNummer) VALUES ({jahr}, 1) ON DUPLICATE KEY UPDATE LetzteNummer = LetzteNummer + 1;",
            cancellationToken);
        var nummer = (await db.AktenzeichenZaehler.AsNoTracking()
            .FirstAsync(z => z.Jahr == jahr, cancellationToken)).LetzteNummer;

        var person = new Person
        {
            Aktenzeichen = $"NOOSE-P-{jahr}-{nummer:0000}",
            Name = eingabe.Name.Trim(),
            Beschreibung = Leer(eingabe.Beschreibung),
            Lebensstatus = eingabe.Lebensstatus,
            TotBis = eingabe.Lebensstatus == Lebensstatus.Tot ? LebensstatusLogic.TotBisAb(DateTime.UtcNow) : null,
            Einstufung = eingabe.Einstufung,
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };
        KinderMappen(person, eingabe);

        if (eingabe.Einstufung != Einstufung.Unbekannt)
        {
            person.EinstufungVerlauf.Add(VerlaufEintrag(person.Id, eingabe.Einstufung, eingabe.EinstufungBegruendung, handelnder));
        }

        db.Personen.Add(person);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return person;
    }

    public async Task AktualisierenAsync(string id, PersonEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
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

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");
        // Hard-Delete wird vom Interceptor in Soft-Delete umgewandelt (+ Audit „Geloescht").
        db.Personen.Remove(person);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
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
        if (neu == Einstufung.GesichertStaatsgefaehrdend && !handelnder.DarfHoechsteEinstufung())
        {
            throw new InvalidOperationException(
                "'Gesichert staatsgefährdend' darf erst ab Senior Special Agent direkt gesetzt werden – sonst per Antrag (Phase 5).");
        }

        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        person.Einstufung = neu;
        db.EinstufungVerlauf.Add(VerlaufEintrag(id, neu, begruendung, handelnder));
        await db.SaveChangesAsync(cancellationToken);
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
        db.PersonFotos.Add(foto);
        await db.SaveChangesAsync(cancellationToken);
        return foto;
    }

    public async Task FotoEntfernenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
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

    public Task<PersonFoto?> GetFotoMitPersonAsync(string fotoId, CancellationToken cancellationToken = default)
        => db.PersonFotos.Include(f => f.Person).FirstOrDefaultAsync(f => f.Id == fotoId, cancellationToken);

    public async Task<List<AuditLog>> GetHistorieAsync(string personId, CancellationToken cancellationToken = default)
    {
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

    private static EinstufungVerlauf VerlaufEintrag(string personId, Einstufung wert, string? begruendung, ClaimsPrincipal handelnder)
        => new()
        {
            PersonId = personId,
            Wert = wert,
            Begruendung = Leer(begruendung),
            Zeitpunkt = DateTime.UtcNow,
            AgentId = handelnder.GetAgentId(),
            AgentName = handelnder.GetAnzeigename(),
        };

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
