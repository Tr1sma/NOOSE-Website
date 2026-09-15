using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Changelog;

namespace NOOSE_Website.Infrastructure.Changelog;

/// <summary>Writes the shipped changelog into the database, idempotently, without ever overwriting an edit.</summary>
/// <remarks>
/// Four rules, in the order they matter:
/// a line nobody has touched is kept current with the shipped text; a line somebody edited is never written again;
/// a line somebody deleted is not revived; and a line that was never shipped (no seed key) is invisible to this class.
/// <para>
/// The build number is stamped here rather than authored: it is unknown while the lines are written. The newest
/// release gets the running build on the first start after it shipped; an older one stays unnumbered for good.
/// </para>
/// <para>
/// Seed through a context that carries the audit interceptor. The release CreatedAt it stamps is what the login hint
/// compares against; a bare context leaves it at default and the hint then reports nothing, silently and forever.
/// </para>
/// </remarks>
public static class ChangelogSeeder
{
    public static Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
        => SeedAsync(db, ChangelogContent.Releases, ChangelogContent.Revision, null, cancellationToken);

    /// <summary>The same pass over an explicit list; the tests drive the revision rule through this overload.</summary>
    /// <param name="buildNumber">Build to stamp, or null to read the running assembly.</param>
    public static async Task SeedAsync(
        AppDbContext db,
        IReadOnlyList<ChangelogContent.SeededRelease> releases,
        int revision,
        string? buildNumber,
        CancellationToken cancellationToken = default)
    {
        await RenameLegacyVersionsAsync(db, releases, cancellationToken);
        await SeedReleasesAsync(db, releases, cancellationToken);
        var releaseIdByVersion = await db.Aenderungsfassungen
            .Select(r => new { r.Version, r.Id })
            .ToDictionaryAsync(r => r.Version, r => r.Id, StringComparer.Ordinal, cancellationToken);

        await RenameLegacyEntryKeysAsync(db, releases, releaseIdByVersion, cancellationToken);
        await SeedEntriesAsync(db, releases, revision, releaseIdByVersion, cancellationToken);
        await StampBuildNumberAsync(db, buildNumber ?? RunningBuild(), cancellationToken);
    }

    private static async Task RenameLegacyVersionsAsync(
        AppDbContext db,
        IReadOnlyList<ChangelogContent.SeededRelease> releases,
        CancellationToken cancellationToken)
    {
        var rows = await db.Aenderungsfassungen.ToListAsync(cancellationToken);
        var byVersion = rows.ToDictionary(r => r.Version, StringComparer.Ordinal);
        var changed = false;

        foreach (var release in releases.Where(r => r.LegacyVersion is not null))
        {
            if (byVersion.ContainsKey(release.Version)
                || !byVersion.TryGetValue(release.LegacyVersion!, out var row))
            {
                continue;
            }

            byVersion.Remove(row.Version);
            row.Version = release.Version;
            byVersion.Add(row.Version, row);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task RenameLegacyEntryKeysAsync(
        AppDbContext db,
        IReadOnlyList<ChangelogContent.SeededRelease> releases,
        IReadOnlyDictionary<string, string> releaseIdByVersion,
        CancellationToken cancellationToken)
    {
        var rows = await db.Aenderungseintraege
            .IgnoreQueryFilters()
            .Where(e => e.SeedKey != null)
            .ToListAsync(cancellationToken);
        var byKey = rows.ToDictionary(e => e.SeedKey!, StringComparer.Ordinal);
        var changed = false;

        foreach (var release in releases.Where(r => r.LegacyVersion is not null))
        {
            if (!releaseIdByVersion.TryGetValue(release.Version, out var releaseId))
            {
                continue;
            }

            foreach (var shipped in release.Entries)
            {
                var suffixStart = shipped.Key.IndexOf('-');
                if (suffixStart < 0)
                {
                    continue;
                }
                var legacyKey = release.LegacyVersion + shipped.Key[suffixStart..];
                if (byKey.ContainsKey(shipped.Key) || !byKey.TryGetValue(legacyKey, out var row))
                {
                    continue;
                }

                byKey.Remove(legacyKey);
                row.SeedKey = shipped.Key;
                row.ReleaseId = releaseId;
                byKey.Add(shipped.Key, row);
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedReleasesAsync(
        AppDbContext db,
        IReadOnlyList<ChangelogContent.SeededRelease> releases,
        CancellationToken cancellationToken)
    {
        var known = await db.Aenderungsfassungen
            .Select(r => r.Version)
            .ToListAsync(cancellationToken);

        var added = false;
        for (var i = 0; i < releases.Count; i++)
        {
            var r = releases[i];
            if (known.Contains(r.Version, StringComparer.Ordinal))
            {
                continue;
            }
            db.Aenderungsfassungen.Add(new ChangelogRelease
            {
                Version = r.Version,
                Date = r.Date,
                Title = r.Title,
                // position in the shipped list; only ever the tie-breaker for two releases on one day
                SortOrder = i,
                IsVisible = true,
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedEntriesAsync(
        AppDbContext db,
        IReadOnlyList<ChangelogContent.SeededRelease> releases,
        int revision,
        IReadOnlyDictionary<string, string> releaseIdByVersion,
        CancellationToken cancellationToken)
    {
        // deleted rows count as known: re-creating a line somebody threw away would be a surprise, not a repair
        var existing = await db.Aenderungseintraege
            .IgnoreQueryFilters()
            .Where(e => e.SeedKey != null)
            .ToDictionaryAsync(e => e.SeedKey!, StringComparer.Ordinal, cancellationToken);

        var changed = false;
        foreach (var release in releases)
        {
            if (!releaseIdByVersion.TryGetValue(release.Version, out var releaseId))
            {
                continue;
            }

            for (var i = 0; i < release.Entries.Length; i++)
            {
                var shipped = release.Entries[i];
                var sortOrder = i * 10;

                if (!existing.TryGetValue(shipped.Key, out var row))
                {
                    db.Aenderungseintraege.Add(new ChangelogEntry
                    {
                        ReleaseId = releaseId,
                        Kind = shipped.Kind,
                        Title = shipped.Title,
                        Area = shipped.Area,
                        SortOrder = sortOrder,
                        IsVisible = true,
                        SeedKey = shipped.Key,
                        SeedRevision = revision,
                        IsCustomised = false,
                    });
                    changed = true;
                    continue;
                }

                // an edited row is the author's now, and a deleted one was deleted on purpose
                if (row.IsCustomised || row.IsDeleted || row.SeedRevision >= revision)
                {
                    continue;
                }

                row.ReleaseId = releaseId;
                row.Kind = shipped.Kind;
                row.Title = shipped.Title;
                row.Area = shipped.Area;
                row.SortOrder = sortOrder;
                row.SeedRevision = revision;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task StampBuildNumberAsync(AppDbContext db, string? build, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build))
        {
            return;
        }

        // only the newest release may be stamped: a first start would otherwise walk down the whole
        // list and hand old releases the build number of a deploy that never carried them
        var newest = await db.Aenderungsfassungen
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (newest is null || newest.BuildNumber is not null)
        {
            return;
        }

        newest.BuildNumber = build;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The running build as the status page shows it, without the metadata suffix.</summary>
    private static string? RunningBuild()
    {
        var raw = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }
}
