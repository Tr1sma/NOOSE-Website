using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonDokService" />
public class PersonDokService(IDbContextFactory<AppDbContext> dbFactory, IPersonService personService) : IPersonDokService
{
    public async Task<List<PersonDokAnzeige>> GetFuerPersonAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eigenständige Sichtbarkeitsprüfung der Eltern-Person (nicht nur auf den Aufrufer verlassen).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Person), personId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var doks = await db.PersonDoks
            .Where(d => d.PersonId == personId)
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);
        return await ZuAnzeigeAsync(db, doks, istFuehrung, cancellationToken);
    }

    public async Task<List<PersonDokAnzeige>> GetFuerOrgAsync(string orgTyp, string orgId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Sichtbarkeit der Organisations-Akte selbst prüfen.
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, orgTyp, orgId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var doks = await db.PersonDoks
            .Include(d => d.Person)
            // Person == null → Akte im Papierkorb; Verschlusssache-Personen nur für Führung.
            .Where(d => d.OrgTyp == orgTyp && d.OrgId == orgId
                && d.Person != null && (istFuehrung || !d.Person.IstVerschlusssache))
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);
        return await ZuAnzeigeAsync(db, doks, istFuehrung, cancellationToken);
    }

    public async Task<List<PersonDokAnzeige>> GetAlleAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var doks = await db.PersonDoks
            .Include(d => d.Person)
            // Der Soft-Delete-Filter setzt Person bei gelöschten Akten auf null → solche Doks ausblenden.
            .Where(d => d.Person != null && (istFuehrung || !d.Person.IstVerschlusssache))
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);
        return await ZuAnzeigeAsync(db, doks, istFuehrung, cancellationToken);
    }

    /// <summary>
    /// Reichert geladene Doks mit den Anzeigedaten ihrer verknüpften Organisation an. Namen werden in je
    /// einer Sammelabfrage je Org-Typ aufgelöst und dabei Verschlusssache-gefiltert (<paramref name="istFuehrung"/>);
    /// der globale Soft-Delete-Filter blendet gelöschte Orgs automatisch aus. Nicht (mehr) sichtbare oder
    /// nicht verknüpfte Doks erhalten leere Org-Felder → die Anzeige fällt auf den Freitext zurück.
    /// </summary>
    private static async Task<List<PersonDokAnzeige>> ZuAnzeigeAsync(AppDbContext db, List<PersonDok> doks, bool istFuehrung, CancellationToken cancellationToken)
    {
        var fraktionIds = doks.Where(d => d.OrgTyp == nameof(Fraktion) && d.OrgId is not null).Select(d => d.OrgId!).Distinct().ToList();
        var gruppenIds = doks.Where(d => d.OrgTyp == nameof(Personengruppe) && d.OrgId is not null).Select(d => d.OrgId!).Distinct().ToList();

        var fraktionen = new Dictionary<string, (string Name, string Aktenzeichen)>();
        if (fraktionIds.Count > 0)
        {
            fraktionen = await db.Fraktionen
                .Where(f => fraktionIds.Contains(f.Id) && (istFuehrung || !f.IstVerschlusssache))
                .Select(f => new { f.Id, f.Name, f.Aktenzeichen })
                .ToDictionaryAsync(f => f.Id, f => (f.Name, f.Aktenzeichen), cancellationToken);
        }

        var gruppen = new Dictionary<string, (string Name, string Aktenzeichen)>();
        if (gruppenIds.Count > 0)
        {
            gruppen = await db.Personengruppen
                .Where(g => gruppenIds.Contains(g.Id) && (istFuehrung || !g.IstVerschlusssache))
                .Select(g => new { g.Id, g.Name, g.Aktenzeichen })
                .ToDictionaryAsync(g => g.Id, g => (g.Name, g.Aktenzeichen), cancellationToken);
        }

        return doks.Select(d =>
        {
            if (d.OrgId is not null && d.OrgTyp == nameof(Fraktion) && fraktionen.TryGetValue(d.OrgId, out var f))
            {
                return new PersonDokAnzeige(d, f.Name, f.Aktenzeichen, $"/fraktionen/{d.OrgId}");
            }
            if (d.OrgId is not null && d.OrgTyp == nameof(Personengruppe) && gruppen.TryGetValue(d.OrgId, out var g))
            {
                return new PersonDokAnzeige(d, g.Name, g.Aktenzeichen, $"/personengruppen/{d.OrgId}");
            }
            return new PersonDokAnzeige(d, null, null, null);
        }).ToList();
    }

    public async Task<PersonDok> ErstellenAsync(string personId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");
        if (person.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        return await ErstelleDokAsync(db, personId, eingabe, cancellationToken);
    }

    public async Task<PersonDok> ErstellenFuerNeuePersonAsync(string name, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Für eine neue Akte ist ein Name erforderlich.");
        }

        // Neue Akte (nur Name) über den Personen-Dienst anlegen – inkl. Aktenzeichen-Vergabe und Audit –
        // und das Dok daran hängen. Jeder Dienst nutzt seinen eigenen Context aus der Factory; die Person
        // ist nach ErstellenAsync committet und wird unten in unserem Context frisch geladen.
        var person = await personService.ErstellenAsync(new PersonEingabe { Name = name.Trim() }, handelnder, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ErstelleDokAsync(db, person.Id, eingabe, cancellationToken);
    }

    public async Task<PersonDok> AktualisierenAsync(string dokId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dok = await db.PersonDoks
            .Include(d => d.Person)
            .FirstOrDefaultAsync(d => d.Id == dokId, cancellationToken)
            ?? throw new InvalidOperationException($"Dok '{dokId}' nicht gefunden.");

        if (dok.Person?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Alten Zustand merken, bevor wir überschreiben – die Status-Neuauswertung braucht beides.
        var altAusgang = dok.Ausgang;
        var altZeitpunkt = dok.Zeitpunkt;

        dok.Zeitpunkt = eingabe.Zeitpunkt;
        dok.Grund = Leer(eingabe.Grund);
        dok.Fraktion = Leer(eingabe.Fraktion);
        var aktOrgId = Leer(eingabe.OrgId);
        dok.OrgId = aktOrgId;
        // Kein verwaister Typ ohne Id (Freitext-Fallback).
        dok.OrgTyp = aktOrgId is null ? null : Leer(eingabe.OrgTyp);
        dok.ErhalteneInformationen = Leer(eingabe.ErhalteneInformationen);
        dok.Wahrheitsserum = eingabe.Wahrheitsserum;
        dok.Ausgang = eingabe.Ausgang;
        // Gedächtnisverlust folgt dem Ausgang (Amnestie-Spritze).
        dok.GedaechtnisGeloescht = eingabe.Ausgang == MassnahmeAusgang.Spritze;

        // Person ist null, wenn ihre Akte (soft-)gelöscht ist – dann ist der Lebensstatus ohnehin belanglos.
        if (dok.Person is not null)
        {
            StatusNeuAuswerten(dok.Person, altAusgang, altZeitpunkt, eingabe);
        }

        // Dok + ggf. Person in einem SaveChanges → Audit setzt GeaendertAm/Von automatisch.
        await db.SaveChangesAsync(cancellationToken);
        return dok;
    }

    /// <summary>
    /// Wertet den Status-Effekt eines bearbeiteten Doks neu aus („Status neu anwenden"). Ein Dok „besitzt"
    /// das aktuelle Tot-Fenster nur, wenn dieses aus seinem (alten) Zeitpunkt stammt – so wird ein manuell
    /// oder von einem anderen Dok gesetzter Lebensstatus nicht überschrieben.
    /// </summary>
    private static void StatusNeuAuswerten(Person person, MassnahmeAusgang altAusgang, DateTime altZeitpunkt, PersonDokEingabe neu)
    {
        var altWarErschossen = altAusgang == MassnahmeAusgang.Erschossen;
        var neuIstErschossen = neu.Ausgang == MassnahmeAusgang.Erschossen;
        var besitztFenster = person.Lebensstatus == Lebensstatus.Tot
            && person.TotBis == LebensstatusLogic.TotBisAb(altZeitpunkt);

        if (neuIstErschossen)
        {
            if (!altWarErschossen)
            {
                // Neu „Erschossen": Tod zum Maßnahme-Zeitpunkt setzen (wie beim Anlegen).
                person.Lebensstatus = Lebensstatus.Tot;
                person.TotBis = LebensstatusLogic.TotBisAb(neu.Zeitpunkt);
            }
            else if (besitztFenster)
            {
                // Bleibt „Erschossen", Zeitpunkt evtl. verschoben → eigenes Tot-Fenster nachführen.
                person.TotBis = LebensstatusLogic.TotBisAb(neu.Zeitpunkt);
            }
        }
        else if (altWarErschossen && besitztFenster)
        {
            // Weg von „Erschossen": das von diesem Dok gesetzte Tot-Fenster zurücknehmen.
            person.Lebensstatus = Lebensstatus.Lebend;
            person.TotBis = null;
        }
    }

    private async Task<PersonDok> ErstelleDokAsync(AppDbContext db, string personId, PersonDokEingabe eingabe, CancellationToken cancellationToken)
    {
        var orgId = Leer(eingabe.OrgId);
        var dok = new PersonDok
        {
            PersonId = personId,
            Zeitpunkt = eingabe.Zeitpunkt,
            Grund = Leer(eingabe.Grund),
            Fraktion = Leer(eingabe.Fraktion),
            OrgId = orgId,
            // Kein verwaister Typ ohne Id (Freitext-Fallback).
            OrgTyp = orgId is null ? null : Leer(eingabe.OrgTyp),
            ErhalteneInformationen = Leer(eingabe.ErhalteneInformationen),
            Wahrheitsserum = eingabe.Wahrheitsserum,
            Ausgang = eingabe.Ausgang,
        };

        // Automatik: Maßnahme-Ausgang wirkt auf den Lebensstatus der Person.
        switch (eingabe.Ausgang)
        {
            case MassnahmeAusgang.Erschossen:
                // Tod tritt zum Maßnahme-Zeitpunkt ein; 20-Minuten-Fenster bis zum Respawn. Die Person im
                // selben Context laden, damit die Statusänderung mit dem Dok gespeichert wird.
                var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken);
                if (person is not null)
                {
                    var neuTotBis = LebensstatusLogic.TotBisAb(eingabe.Zeitpunkt);
                    // Ein bereits laufendes, späteres Tot-Fenster nicht verkürzen (z. B. wenn nachträglich ein
                    // älteres Dok erfasst wird) – nur setzen/verlängern.
                    if (person.TotBis is null || neuTotBis > person.TotBis)
                    {
                        person.Lebensstatus = Lebensstatus.Tot;
                        person.TotBis = neuTotBis;
                    }
                }
                break;
            case MassnahmeAusgang.Spritze:
                // Amnestie-Spritze: Person lebt weiter, verliert aber ihre Erinnerung.
                dok.GedaechtnisGeloescht = true;
                break;
        }

        db.PersonDoks.Add(dok);
        // Person + Dok in einem SaveChanges → je ein Audit-Eintrag (Dok „Erstellt", Person „Geaendert").
        await db.SaveChangesAsync(cancellationToken);
        return dok;
    }

    public async Task LoeschenAsync(string dokId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dok = await db.PersonDoks.Include(d => d.Person).FirstOrDefaultAsync(d => d.Id == dokId, cancellationToken);
        if (dok is null)
        {
            return;
        }
        if (dok.Person?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Hat dieses „Erschossen"-Dok das aktuelle Tot-Fenster gesetzt, beim Löschen den Status zurücknehmen
        // (sonst bliebe die versehentlich getötete Person für den Rest des Fensters „Tot").
        if (dok.Person is not null && dok.Ausgang == MassnahmeAusgang.Erschossen
            && dok.Person.Lebensstatus == Lebensstatus.Tot
            && dok.Person.TotBis == LebensstatusLogic.TotBisAb(dok.Zeitpunkt))
        {
            dok.Person.Lebensstatus = Lebensstatus.Lebend;
            dok.Person.TotBis = null;
        }

        // Soft-Delete via Interceptor.
        db.PersonDoks.Remove(dok);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
