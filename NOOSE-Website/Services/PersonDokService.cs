using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonDokService" />
public class PersonDokService(IDbContextFactory<AppDbContext> dbFactory, IPersonService personService) : IPersonDokService
{
    public async Task<List<PersonDok>> GetFuerPersonAsync(string personId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonDoks
            .Where(d => d.PersonId == personId)
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonDok>> GetAlleAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonDoks
            .Include(d => d.Person)
            // Der Soft-Delete-Filter setzt Person bei gelöschten Akten auf null → solche Doks ausblenden.
            .Where(d => d.Person != null && (istFuehrung || !d.Person.IstVerschlusssache))
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonDok> ErstellenAsync(string personId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Personen.AnyAsync(p => p.Id == personId, cancellationToken))
        {
            throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");
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

        // Alten Zustand merken, bevor wir überschreiben – die Status-Neuauswertung braucht beides.
        var altAusgang = dok.Ausgang;
        var altZeitpunkt = dok.Zeitpunkt;

        dok.Zeitpunkt = eingabe.Zeitpunkt;
        dok.Grund = Leer(eingabe.Grund);
        dok.Fraktion = Leer(eingabe.Fraktion);
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
        var dok = new PersonDok
        {
            PersonId = personId,
            Zeitpunkt = eingabe.Zeitpunkt,
            Grund = Leer(eingabe.Grund),
            Fraktion = Leer(eingabe.Fraktion),
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
                    person.Lebensstatus = Lebensstatus.Tot;
                    person.TotBis = LebensstatusLogic.TotBisAb(eingabe.Zeitpunkt);
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
        var dok = await db.PersonDoks.FirstOrDefaultAsync(d => d.Id == dokId, cancellationToken);
        if (dok is null)
        {
            return;
        }
        // Soft-Delete via Interceptor; ein evtl. ausgelöster Status bleibt unverändert (kein Revert).
        db.PersonDoks.Remove(dok);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
