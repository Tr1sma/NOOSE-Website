using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonDokService" />
public class PersonDokService(AppDbContext db, IPersonService personService) : IPersonDokService
{
    public Task<List<PersonDok>> GetFuerPersonAsync(string personId, CancellationToken cancellationToken = default)
        => db.PersonDoks
            .Where(d => d.PersonId == personId)
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);

    public Task<List<PersonDok>> GetAlleAsync(bool istFuehrung, CancellationToken cancellationToken = default)
        => db.PersonDoks
            .Include(d => d.Person)
            // Der Soft-Delete-Filter setzt Person bei gelöschten Akten auf null → solche Doks ausblenden.
            .Where(d => d.Person != null && (istFuehrung || !d.Person.IstVerschlusssache))
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);

    public async Task<PersonDok> ErstellenAsync(string personId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");

        return await ErstelleDokAsync(person, eingabe, cancellationToken);
    }

    public async Task<PersonDok> ErstellenFuerNeuePersonAsync(string name, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Für eine neue Akte ist ein Name erforderlich.");
        }

        // Neue Akte (nur Name) über den Personen-Dienst anlegen – inkl. Aktenzeichen-Vergabe und Audit –
        // und das Dok daran hängen. Beide Schritte laufen über denselben (scoped) DbContext.
        var person = await personService.ErstellenAsync(new PersonEingabe { Name = name.Trim() }, handelnder, cancellationToken);
        return await ErstelleDokAsync(person, eingabe, cancellationToken);
    }

    private async Task<PersonDok> ErstelleDokAsync(Person person, PersonDokEingabe eingabe, CancellationToken cancellationToken)
    {
        var dok = new PersonDok
        {
            PersonId = person.Id,
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
                // Tod tritt zum Maßnahme-Zeitpunkt ein; 20-Minuten-Fenster bis zum Respawn.
                person.Lebensstatus = Lebensstatus.Tot;
                person.TotBis = LebensstatusLogic.TotBisAb(eingabe.Zeitpunkt);
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
