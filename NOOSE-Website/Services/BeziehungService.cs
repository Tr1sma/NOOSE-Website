using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBeziehungService" />
public class BeziehungService(IDbContextFactory<AppDbContext> dbFactory, IBedrohungsScoreService bedrohung) : IBeziehungService
{
    public async Task<List<BeziehungAnzeige>> GetFuerPersonAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var roh = await db.PersonBeziehungen
            .Include(b => b.PersonA)
            .Include(b => b.PersonB)
            .Where(b => b.PersonAId == personId || b.PersonBId == personId)
            .OrderByDescending(b => b.ErstelltAm)
            .ToListAsync(cancellationToken);

        var ergebnis = new List<BeziehungAnzeige>();
        foreach (var b in roh)
        {
            // Die jeweils andere Person bestimmen.
            var andere = b.PersonAId == personId ? b.PersonB : b.PersonA;
            if (andere is null)
            {
                continue; // Gegenseite im Papierkorb (Query-Filter) → ausblenden.
            }
            if (andere.IstVerschlusssache && !istFuehrung)
            {
                continue;
            }
            ergebnis.Add(new BeziehungAnzeige(b.Id, b.Typ, b.Notiz, andere.Id, andere.Name, andere.Aktenzeichen));
        }
        return ergebnis;
    }

    public async Task ErstellenAsync(string personAId, string personBId, BeziehungsTyp typ, string? notiz, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personBId) || personAId == personBId)
        {
            throw new InvalidOperationException("Bitte eine andere Person als Gegenüber auswählen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Personen.AnyAsync(p => p.Id == personBId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Person wurde nicht gefunden.");
        }

        // Dieselbe Beziehung (gleicher Typ) zwischen beiden Personen nicht doppelt anlegen – in beiden
        // Richtungen geprüft. Andere Beziehungstypen zwischen denselben Personen bleiben erlaubt.
        var existiert = await db.PersonBeziehungen.AnyAsync(b => b.Typ == typ
            && ((b.PersonAId == personAId && b.PersonBId == personBId)
             || (b.PersonAId == personBId && b.PersonBId == personAId)),
            cancellationToken);
        if (existiert)
        {
            throw new InvalidOperationException("Eine Beziehung dieses Typs besteht bereits zwischen den beiden Personen.");
        }

        db.PersonBeziehungen.Add(new PersonBeziehung
        {
            PersonAId = personAId,
            PersonBId = personBId,
            Typ = typ,
            Notiz = string.IsNullOrWhiteSpace(notiz) ? null : notiz.Trim(),
        });
        await db.SaveChangesAsync(cancellationToken);
        // Feind/Verbündeter/Geschäftspartner wirken auf P4 BEIDER Personen → beide neu berechnen.
        await bedrohung.NeuBerechnenPersonScoreAsync(personAId, cancellationToken);
        await bedrohung.NeuBerechnenPersonScoreAsync(personBId, cancellationToken);
    }

    public async Task EntfernenAsync(string beziehungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var b = await db.PersonBeziehungen.FirstOrDefaultAsync(x => x.Id == beziehungId, cancellationToken);
        if (b is null)
        {
            return;
        }
        var personAId = b.PersonAId;
        var personBId = b.PersonBId;
        db.PersonBeziehungen.Remove(b); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
        // Entfernte Beziehung wirkt auf P4 beider Personen → beide neu berechnen.
        await bedrohung.NeuBerechnenPersonScoreAsync(personAId, cancellationToken);
        await bedrohung.NeuBerechnenPersonScoreAsync(personBId, cancellationToken);
    }
}
