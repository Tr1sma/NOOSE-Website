using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVerknuepfungService" />
public class VerknuepfungService(IDbContextFactory<AppDbContext> dbFactory) : IVerknuepfungService
{
    public async Task<List<VerknuepfungAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, VerknuepfungArt? art = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await AkteSichtbarAsync(db, entitaetTyp, entitaetId, istFuehrung, cancellationToken))
        {
            return new();
        }

        var query = db.Verknuepfungen
            .Where(v => (v.VonTyp == entitaetTyp && v.VonId == entitaetId)
                     || (v.NachTyp == entitaetTyp && v.NachId == entitaetId));
        if (art is not null)
        {
            query = query.Where(v => v.Art == art.Value);
        }
        var roh = await query
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

        // Ziele für Anzeige + Sichtbarkeitsfilter auflösen (Person + Fraktion).
        var personIds = paare.Where(p => p.AndereTyp == nameof(Person)).Select(p => p.AndereId).Distinct().ToList();
        var personMap = (await db.Personen
            .Where(p => personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache })
            .ToListAsync(cancellationToken)).ToDictionary(p => p.Id);

        var fraktionIds = paare.Where(p => p.AndereTyp == nameof(Fraktion)).Select(p => p.AndereId).Distinct().ToList();
        var fraktionMap = (await db.Fraktionen
            .Where(f => fraktionIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.IstVerschlusssache })
            .ToListAsync(cancellationToken)).ToDictionary(f => f.Id);

        var gruppenIds = paare.Where(p => p.AndereTyp == nameof(Personengruppe)).Select(p => p.AndereId).Distinct().ToList();
        var gruppenMap = (await db.Personengruppen
            .Where(g => gruppenIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.Aktenzeichen, g.IstVerschlusssache })
            .ToListAsync(cancellationToken)).ToDictionary(g => g.Id);

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
                    $"{person.Name} ({person.Aktenzeichen})", p.V.Automatisch));
            }
            else if (p.AndereTyp == nameof(Fraktion))
            {
                if (!fraktionMap.TryGetValue(p.AndereId, out var fraktion))
                {
                    continue;
                }
                if (fraktion.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label,
                    $"{fraktion.Name} ({fraktion.Aktenzeichen})", p.V.Automatisch));
            }
            else if (p.AndereTyp == nameof(Personengruppe))
            {
                if (!gruppenMap.TryGetValue(p.AndereId, out var gruppe))
                {
                    continue;
                }
                if (gruppe.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label,
                    $"{gruppe.Name} ({gruppe.Aktenzeichen})", p.V.Automatisch));
            }
            else
            {
                // Andere Aktentypen folgen in späteren Phasen – vorerst Rohbezeichnung.
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label, p.AndereId, p.V.Automatisch));
            }
        }
        return ergebnis;
    }

    public async Task ErstellenAsync(string vonTyp, string vonId, string nachTyp, string nachId, string? label, ClaimsPrincipal handelnder, VerknuepfungArt art = VerknuepfungArt.Standard, CancellationToken cancellationToken = default)
    {
        if (vonTyp == nachTyp && vonId == nachId)
        {
            throw new InvalidOperationException("Eine Akte kann nicht mit sich selbst verknüpft werden.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Verknuepfungen.Add(new Verknuepfung
        {
            VonTyp = vonTyp,
            VonId = vonId,
            NachTyp = nachTyp,
            NachId = nachId,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Art = art,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EntfernenAsync(string verknuepfungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var v = await db.Verknuepfungen.FirstOrDefaultAsync(x => x.Id == verknuepfungId, cancellationToken);
        if (v is null)
        {
            return;
        }
        db.Verknuepfungen.Remove(v); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Eltern-Sichtbarkeit ohne FK-Navigation prüfen (Person + Fraktion); vgl. QuelleService.</summary>
    private static async Task<bool> AkteSichtbarAsync(AppDbContext db, string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken)
    {
        if (entitaetTyp == nameof(Person))
        {
            var person = await db.Personen
                .Where(p => p.Id == entitaetId)
                .Select(p => new { p.IstVerschlusssache })
                .FirstOrDefaultAsync(cancellationToken);
            return person is not null && (istFuehrung || !person.IstVerschlusssache);
        }
        if (entitaetTyp == nameof(Fraktion))
        {
            var fraktion = await db.Fraktionen
                .Where(f => f.Id == entitaetId)
                .Select(f => new { f.IstVerschlusssache })
                .FirstOrDefaultAsync(cancellationToken);
            return fraktion is not null && (istFuehrung || !fraktion.IstVerschlusssache);
        }
        if (entitaetTyp == nameof(Personengruppe))
        {
            var gruppe = await db.Personengruppen
                .Where(g => g.Id == entitaetId)
                .Select(g => new { g.IstVerschlusssache })
                .FirstOrDefaultAsync(cancellationToken);
            return gruppe is not null && (istFuehrung || !gruppe.IstVerschlusssache);
        }
        return true;
    }
}
