using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ICustomFeldWertService" />
public class CustomFeldWertService(IDbContextFactory<AppDbContext> dbFactory) : ICustomFeldWertService
{
    public async Task<List<CustomFeldWertAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var definitionen = await db.CustomFeldDefinitionen
            .Where(d => d.EntitaetTyp == entitaetTyp && d.IstAktiv)
            .OrderBy(d => d.Reihenfolge).ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
        if (definitionen.Count == 0)
        {
            return new List<CustomFeldWertAnzeige>();
        }

        var werte = await db.CustomFeldWerte
            .Where(w => w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId)
            .ToListAsync(cancellationToken);
        var wertJeDef = werte
            .GroupBy(w => w.CustomFeldDefinitionId)
            .ToDictionary(g => g.Key, g => g.First().Wert);

        return definitionen
            .Select(d => new CustomFeldWertAnzeige
            {
                Definition = d,
                Wert = wertJeDef.GetValueOrDefault(d.Id),
            })
            .ToList();
    }

    public async Task SetzenAsync(string entitaetTyp, string entitaetId, IReadOnlyDictionary<string, string?> werteJeDefinition,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var definitionen = await db.CustomFeldDefinitionen
            .Where(d => d.EntitaetTyp == entitaetTyp && d.IstAktiv)
            .ToListAsync(cancellationToken);

        // Pflichtfeld-Validierung
        foreach (var def in definitionen.Where(d => d.Pflicht))
        {
            var wert = werteJeDefinition.GetValueOrDefault(def.Id);
            if (string.IsNullOrWhiteSpace(wert))
            {
                throw new InvalidOperationException($"Das Pflichtfeld „{def.Name}“ muss ausgefüllt werden.");
            }
        }

        var bestehende = await db.CustomFeldWerte
            .Where(w => w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId)
            .ToListAsync(cancellationToken);
        var bestehendJeDef = bestehende.ToDictionary(w => w.CustomFeldDefinitionId);

        foreach (var def in definitionen)
        {
            var neu = werteJeDefinition.GetValueOrDefault(def.Id).TrimToNull();
            bestehendJeDef.TryGetValue(def.Id, out var vorhanden);

            if (neu is null)
            {
                if (vorhanden is not null)
                {
                    db.CustomFeldWerte.Remove(vorhanden);
                }
                continue;
            }

            if (vorhanden is null)
            {
                db.CustomFeldWerte.Add(new CustomFeldWert
                {
                    CustomFeldDefinitionId = def.Id,
                    EntitaetTyp = entitaetTyp,
                    EntitaetId = entitaetId,
                    Wert = neu,
                });
            }
            else if (vorhanden.Wert != neu)
            {
                vorhanden.Wert = neu;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
