using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISteckbriefVorschlagService" />
public class SteckbriefVorschlagService(IDbContextFactory<AppDbContext> dbFactory) : ISteckbriefVorschlagService
{
    public async Task<IReadOnlyList<string>> GetAsync(VorschlagTyp typ, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SteckbriefVorschlaege
            .Where(v => v.Typ == typ)
            .OrderBy(v => v.Wert)
            .Select(v => v.Wert)
            .ToListAsync(cancellationToken);
    }

    public async Task VormerkenAsync(AppDbContext db, VorschlagTyp typ, IEnumerable<string> werte, CancellationToken cancellationToken = default)
    {
        // Eingaben normalisieren: trimmen, Leere verwerfen, case-insensitiv deduplizieren.
        var kandidaten = werte
            .Select(w => w?.Trim() ?? string.Empty)
            .Where(w => w.Length > 0)
            .GroupBy(w => w.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        if (kandidaten.Count == 0)
        {
            return;
        }

        // Bereits vorhandene Werte (case-insensitiv) ermitteln, damit nur wirklich Neues angelegt wird.
        var kandidatenLower = kandidaten.Select(w => w.ToLowerInvariant()).ToList();
        var vorhanden = await db.SteckbriefVorschlaege
            .Where(v => v.Typ == typ && kandidatenLower.Contains(v.Wert.ToLower()))
            .Select(v => v.Wert)
            .ToListAsync(cancellationToken);
        var vorhandenSet = vorhanden.Select(w => w.ToLowerInvariant()).ToHashSet();

        foreach (var wert in kandidaten)
        {
            // vorhandenSet wächst mit → fängt auch identische Werte innerhalb desselben Aufrufs ab.
            if (vorhandenSet.Add(wert.ToLowerInvariant()))
            {
                // Nur vormerken – der Aufrufer (PersonService) speichert im selben SaveChanges.
                db.SteckbriefVorschlaege.Add(new SteckbriefVorschlag { Typ = typ, Wert = wert });
            }
        }
    }
}
