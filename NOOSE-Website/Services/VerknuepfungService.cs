using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVerknuepfungService" />
public class VerknuepfungService(AppDbContext db) : IVerknuepfungService
{
    public async Task<List<VerknuepfungAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        if (!await AkteSichtbarAsync(entitaetTyp, entitaetId, istFuehrung, cancellationToken))
        {
            return new();
        }

        var roh = await db.Verknuepfungen
            .Where(v => (v.VonTyp == entitaetTyp && v.VonId == entitaetId)
                     || (v.NachTyp == entitaetTyp && v.NachId == entitaetId))
            .OrderByDescending(v => v.ErstelltAm)
            .ToListAsync(cancellationToken);

        // Je Verknüpfung die „andere Seite" relativ zur betrachteten Akte bestimmen.
        var paare = roh.Select(v =>
        {
            var istVon = v.VonTyp == entitaetTyp && v.VonId == entitaetId;
            return (V: v,
                    AndereTyp: istVon ? v.NachTyp : v.VonTyp,
                    AndereId: istVon ? v.NachId : v.VonId);
        }).ToList();

        // Personen-Ziele für Anzeige + Sichtbarkeitsfilter auflösen (Phase 3: nur Person).
        var personIds = paare.Where(p => p.AndereTyp == nameof(Person)).Select(p => p.AndereId).Distinct().ToList();
        var personen = await db.Personen
            .Where(p => personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache })
            .ToListAsync(cancellationToken);
        var personMap = personen.ToDictionary(p => p.Id);

        var ergebnis = new List<VerknuepfungAnzeige>();
        foreach (var p in paare)
        {
            if (p.AndereTyp == nameof(Person))
            {
                if (!personMap.TryGetValue(p.AndereId, out var person))
                {
                    continue; // Ziel im Papierkorb oder unbekannt → ausblenden.
                }
                if (person.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label,
                    $"{person.Name} ({person.Aktenzeichen})"));
            }
            else
            {
                // Andere Aktentypen folgen ab Phase 4 – vorerst Rohbezeichnung.
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label, p.AndereId));
            }
        }
        return ergebnis;
    }

    public async Task ErstellenAsync(string vonTyp, string vonId, string nachTyp, string nachId, string? label, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (vonTyp == nachTyp && vonId == nachId)
        {
            throw new InvalidOperationException("Eine Akte kann nicht mit sich selbst verknüpft werden.");
        }

        db.Verknuepfungen.Add(new Verknuepfung
        {
            VonTyp = vonTyp,
            VonId = vonId,
            NachTyp = nachTyp,
            NachId = nachId,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EntfernenAsync(string verknuepfungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var v = await db.Verknuepfungen.FirstOrDefaultAsync(x => x.Id == verknuepfungId, cancellationToken);
        if (v is null)
        {
            return;
        }
        db.Verknuepfungen.Remove(v); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Eltern-Sichtbarkeit ohne FK-Navigation prüfen (nur Person in Phase 3); vgl. QuelleService.</summary>
    private async Task<bool> AkteSichtbarAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken)
    {
        if (entitaetTyp != nameof(Person))
        {
            return true;
        }
        var person = await db.Personen
            .Where(p => p.Id == entitaetId)
            .Select(p => new { p.IstVerschlusssache })
            .FirstOrDefaultAsync(cancellationToken);
        return person is not null && (istFuehrung || !person.IstVerschlusssache);
    }
}
