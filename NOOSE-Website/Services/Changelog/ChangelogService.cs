using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Changelog;
using NOOSE_Website.Models.Changelog;

namespace NOOSE_Website.Services.Changelog;

/// <inheritdoc cref="IChangelogService" />
/// <remarks>
/// Reads are open to every internal agent, like the law book: the page says what changed in the tool they all use.
/// Writes are leadership only - a line here is read by everyone and cannot be taken back once seen.
/// </remarks>
public sealed class ChangelogService(IDbContextFactory<AppDbContext> dbFactory) : IChangelogService
{
    public async Task<List<ChangelogReleaseView>> GetTimelineAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var releases = await db.Aenderungsfassungen.AsNoTracking()
            .Where(r => r.IsVisible)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SortOrder)
            .ToListAsync(cancellationToken);
        if (releases.Count == 0)
        {
            return [];
        }

        // flat WHERE IN, not a collection projection: Pomelo translates no lateral join on MySQL
        var ids = releases.Select(r => r.Id).ToList();
        var entries = await db.Aenderungseintraege.AsNoTracking()
            .Where(e => ids.Contains(e.ReleaseId) && e.IsVisible)
            .OrderBy(e => e.SortOrder).ThenBy(e => e.Title)
            .ToListAsync(cancellationToken);
        var byRelease = entries.GroupBy(e => e.ReleaseId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ChangelogReleaseView>();
        foreach (var r in releases)
        {
            // a release whose lines are all hidden or deleted is an empty heading, not a release
            if (!byRelease.TryGetValue(r.Id, out var lines) || lines.Count == 0)
            {
                continue;
            }
            result.Add(new ChangelogReleaseView(r.Id, r.Version, r.Date, r.Title, r.BuildNumber,
                lines.Select(e => new ChangelogEntryView(e.Id, e.Kind, e.Title, e.Area)).ToList()));
        }
        return result;
    }

    public async Task<ChangelogNewsFlash> GetNewsSinceAsync(DateTime? lastSeenUtc, CancellationToken cancellationToken = default)
    {
        // No stamp means the agent has never looked. Announcing the whole history as new since their last visit
        // would be a lie and a wall of text; the caller stamps instead, so the next real release counts.
        if (lastSeenUtc is not { } since)
        {
            return new ChangelogNewsFlash(0, null);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // CreatedAt, not Date: a retroactive release carries a date from the past but arrives now, and one
        // published this morning must still count for somebody who last looked yesterday evening.
        var fresh = await db.Aenderungsfassungen.AsNoTracking()
            .Where(r => r.IsVisible && r.CreatedAt > since)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SortOrder)
            .Select(r => new { r.Id, r.Version })
            .ToListAsync(cancellationToken);
        if (fresh.Count == 0)
        {
            return new ChangelogNewsFlash(0, null);
        }

        var ids = fresh.Select(r => r.Id).ToList();
        var carrying = await db.Aenderungseintraege.AsNoTracking()
            .Where(e => ids.Contains(e.ReleaseId) && e.IsVisible)
            .Select(e => e.ReleaseId)
            .ToListAsync(cancellationToken);
        if (carrying.Count == 0)
        {
            return new ChangelogNewsFlash(0, null);
        }

        // the newest release that actually carries a line, not simply the newest: the page hides a release
        // whose entries are all withdrawn, so naming that one sent the reader looking for a version that is
        // not on /neuerungen at all
        var withEntries = carrying.ToHashSet(StringComparer.Ordinal);
        var newest = fresh.FirstOrDefault(r => withEntries.Contains(r.Id));
        return new ChangelogNewsFlash(carrying.Count, newest?.Version);
    }

    public async Task<List<ChangelogRelease>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Aenderungsfassungen.AsNoTracking()
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ChangelogEntry>> GetEntriesAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Aenderungseintraege.AsNoTracking()
            .Where(e => e.ReleaseId == releaseId)
            .OrderBy(e => e.SortOrder).ThenBy(e => e.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ChangelogEntry>>> GetEntriesAsync(
        IReadOnlyCollection<string> releaseIds, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // one flat WHERE IN for the whole editor, not a round trip per release
        var ids = releaseIds.ToList();
        var entries = await db.Aenderungseintraege.AsNoTracking()
            .Where(e => ids.Contains(e.ReleaseId))
            .OrderBy(e => e.SortOrder).ThenBy(e => e.Title)
            .ToListAsync(cancellationToken);
        return entries.GroupBy(e => e.ReleaseId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ChangelogEntry>)g.ToList());
    }

    public async Task<ChangelogRelease> CreateReleaseAsync(ChangelogReleaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireChangelogWrite(actor);
        var version = CleanVersion(input.Version);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Aenderungsfassungen.AnyAsync(r => r.Version == version, cancellationToken))
        {
            throw new InvalidOperationException($"Die Fassung {version} gibt es bereits.");
        }

        var release = new ChangelogRelease
        {
            Version = version,
            Date = input.Date,
            Title = Clean(input.Title, 200),
            SortOrder = input.SortOrder,
            IsVisible = input.IsVisible,
        };
        db.Aenderungsfassungen.Add(release);
        await db.SaveChangesAsync(cancellationToken);
        return release;
    }

    public async Task RefreshReleaseAsync(string id, ChangelogReleaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireChangelogWrite(actor);
        var version = CleanVersion(input.Version);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var release = await db.Aenderungsfassungen.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Die Fassung wurde nicht gefunden.");
        if (await db.Aenderungsfassungen.AnyAsync(r => r.Id != id && r.Version == version, cancellationToken))
        {
            throw new InvalidOperationException($"Die Fassung {version} gibt es bereits.");
        }

        release.Version = version;
        release.Date = input.Date;
        release.Title = Clean(input.Title, 200);
        release.SortOrder = input.SortOrder;
        release.IsVisible = input.IsVisible;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChangelogEntry> CreateEntryAsync(ChangelogEntryInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireChangelogWrite(actor);
        var title = Clean(input.Title, 300)
            ?? throw new InvalidOperationException("Der Eintrag braucht einen Text.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Aenderungsfassungen.AnyAsync(r => r.Id == input.ReleaseId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Fassung wurde nicht gefunden.");
        }

        var entry = new ChangelogEntry
        {
            ReleaseId = input.ReleaseId,
            Kind = input.Kind,
            Title = title,
            Area = Clean(input.Area, 64),
            SortOrder = input.SortOrder,
            IsVisible = input.IsVisible,
            // hand-written: no seed key, so the shipped list never claims this row
            SeedKey = null,
            IsCustomised = true,
        };
        db.Aenderungseintraege.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task RefreshEntryAsync(string id, ChangelogEntryInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireChangelogWrite(actor);
        var title = Clean(input.Title, 300)
            ?? throw new InvalidOperationException("Der Eintrag braucht einen Text.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Aenderungseintraege.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Der Eintrag wurde nicht gefunden.");
        if (!await db.Aenderungsfassungen.AnyAsync(r => r.Id == input.ReleaseId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Fassung wurde nicht gefunden.");
        }

        entry.ReleaseId = input.ReleaseId;
        entry.Kind = input.Kind;
        entry.Title = title;
        entry.Area = Clean(input.Area, 64);
        entry.SortOrder = input.SortOrder;
        entry.IsVisible = input.IsVisible;
        // the promise of the seeder: once edited here, the shipped list leaves this row alone for good
        entry.IsCustomised = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteEntryAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireChangelogWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Aenderungseintraege.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null)
        {
            return;
        }
        db.Aenderungseintraege.Remove(entry); // soft delete via interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ChangelogEntry>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Aenderungseintraege
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.IsDeleted)
            .OrderByDescending(e => e.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireChangelogWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Aenderungseintraege
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, cancellationToken);
        if (entry is null)
        {
            return;
        }
        entry.IsDeleted = false;
        entry.DeletedAt = null;
        entry.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    private static string CleanVersion(string? value)
    {
        var version = Clean(value, 32);
        var parts = version?.Split('.');
        if (parts is not { Length: 3 }
            || parts[2].Length != 2
            || parts.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit)))
        {
            throw new InvalidOperationException("Die Fassung braucht eine Versionsnummer im Format 2.1.00.");
        }

        return version!;
    }
}
