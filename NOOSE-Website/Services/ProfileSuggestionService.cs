using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISteckbriefVorschlagService" />
public class ProfileSuggestionService(IDbContextFactory<AppDbContext> dbFactory) : IProfileSuggestionService
{
    public async Task<IReadOnlyList<string>> GetAsync(SuggestionType type, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProfileSuggestions
            .Where(v => v.Type == type)
            .OrderBy(v => v.Value)
            .Select(v => v.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task StageAsync(AppDbContext db, SuggestionType type, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        // Eingaben normalisieren: trimmen, Leere verwerfen, case-insensitiv deduplizieren.
        var candidates = values
            .Select(w => w?.Trim() ?? string.Empty)
            .Where(w => w.Length > 0)
            .GroupBy(w => w.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        // Bereits vorhandene Werte (case-insensitiv) ermitteln, damit nur wirklich Neues angelegt wird.
        var candidatesLower = candidates.Select(w => w.ToLowerInvariant()).ToList();
        var exists = await db.ProfileSuggestions
            .Where(v => v.Type == type && candidatesLower.Contains(v.Value.ToLower()))
            .Select(v => v.Value)
            .ToListAsync(cancellationToken);
        var existsSet = exists.Select(w => w.ToLowerInvariant()).ToHashSet();

        foreach (var value in candidates)
        {
            // vorhandenSet wächst mit → fängt auch identische Werte innerhalb desselben Aufrufs ab.
            if (existsSet.Add(value.ToLowerInvariant()))
            {
                // Nur vormerken – der Aufrufer (PersonService) speichert im selben SaveChanges.
                db.ProfileSuggestions.Add(new ProfileSuggestion { Type = type, Value = value });
            }
        }
    }
}
