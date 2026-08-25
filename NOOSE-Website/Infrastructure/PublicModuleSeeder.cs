using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Infrastructure;

/// <summary>Seeds one switch row per catalog module, idempotently.</summary>
/// <remarks>
/// Only missing keys are added; a stored choice is never overwritten. A module that gains its pages in a later phase
/// therefore stays off until someone turns it on, which is the point — nothing goes public by deploying.
/// </remarks>
public static class PublicModuleSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var known = await db.OeffentlicheModule
            .Select(m => m.Key)
            .ToListAsync(cancellationToken);
        var missing = PublicModules.All
            .Where(definition => !known.Contains(definition.Key, StringComparer.Ordinal))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var definition in missing)
        {
            db.OeffentlicheModule.Add(new OeffentlichesModul
            {
                Key = definition.Key,
                IsEnabled = definition.DefaultEnabled,
                SortOrder = definition.SortOrder,
                OfflineText = null,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
