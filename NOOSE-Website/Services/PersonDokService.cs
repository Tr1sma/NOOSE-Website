using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonDokService" />
public class PersonDokService(AppDbContext db) : IPersonDokService
{
    public Task<List<PersonDok>> GetFuerPersonAsync(string personId, CancellationToken cancellationToken = default)
        => db.PersonDoks
            .Where(d => d.PersonId == personId)
            .OrderByDescending(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);

    public async Task<PersonDok> ErstellenAsync(string personId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");

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
