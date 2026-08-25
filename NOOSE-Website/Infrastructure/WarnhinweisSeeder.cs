using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;

namespace NOOSE_Website.Infrastructure;

/// <summary>Puts four starting warning chips into an empty installation.</summary>
/// <remarks>
/// Seeds only while the table is empty — deliberately unlike <see cref="PublicModuleSeeder"/>, which tops up per key.
/// A module key lives in the code and cannot be deleted; a warning is a row that belongs to whoever runs the site, and
/// per-name seeding would resurrect a deleted one on every restart.
/// </remarks>
public static class WarnhinweisSeeder
{
    private static readonly (string Name, string Colour, int SortOrder)[] Starting =
    [
        ("bewaffnet", "Error", 10),
        ("gewaltbereit", "Error", 20),
        ("flieht mit Fahrzeug", "Warning", 30),
        ("nicht selbst eingreifen", "Info", 40),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Warnhinweise.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var (name, colour, sortOrder) in Starting)
        {
            db.Warnhinweise.Add(new Warnhinweis
            {
                Name = name,
                Colour = colour,
                SortOrder = sortOrder,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
