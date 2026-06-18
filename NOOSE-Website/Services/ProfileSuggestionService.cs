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
        // trim, drop empties, dedupe case-insensitively
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

        // find already-present values so only genuinely new ones are added
        var candidatesLower = candidates.Select(w => w.ToLowerInvariant()).ToList();
        var exists = await db.ProfileSuggestions
            .Where(v => v.Type == type && candidatesLower.Contains(v.Value.ToLower()))
            .Select(v => v.Value)
            .ToListAsync(cancellationToken);
        var existsSet = exists.Select(w => w.ToLowerInvariant()).ToHashSet();

        foreach (var value in candidates)
        {
            // set grows as we go, catching duplicates within one call
            if (existsSet.Add(value.ToLowerInvariant()))
            {
                // stage only; caller persists in the same SaveChanges
                db.ProfileSuggestions.Add(new ProfileSuggestion { Type = type, Value = value });
            }
        }
    }
}
