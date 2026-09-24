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
            return ChangelogNewsFlash.None;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Per line, not per release: a release gathers lines over several deploys, and one appended to it this
        // morning is news for somebody who last looked yesterday. CreatedAt, not the release Date, for the same
        // reason a retroactive release counts: it carries a date from the past but arrives now.
        // latest update first, so the preview cap cuts the oldest; one seeding pass shares one stamp and keeps
        // its authored order
        var lines = await db.Aenderungseintraege.AsNoTracking()
            .Where(e => e.IsVisible && e.CreatedAt > since)
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.SortOrder).ThenBy(e => e.Title)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0)
        {
            return ChangelogNewsFlash.None;
        }

        // flat WHERE IN, like the timeline; a hidden release hides its lines here as it does on the page
        var ids = lines.Select(e => e.ReleaseId).Distinct().ToList();
        var releases = await db.Aenderungsfassungen.AsNoTracking()
            .Where(r => ids.Contains(r.Id) && r.IsVisible)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        var byRelease = lines.GroupBy(e => e.ReleaseId).ToDictionary(g => g.Key, g => g.ToList());
        var views = releases
            .Select(r => new ChangelogReleaseView(r.Id, r.Version, r.Date, r.Title, r.BuildNumber,
                byRelease[r.Id].Select(e => new ChangelogEntryView(e.Id, e.Kind, e.Title, e.Area)).ToList()))
            .ToList();
        if (views.Count == 0)
        {
            return ChangelogNewsFlash.None;
        }

        // the newest release that actually carries a new line, not simply the newest: naming one whose lines
        // are all withdrawn sent the reader looking for a version that is not on /neuerungen at all
        return new ChangelogNewsFlash(views.Sum(v => v.Entries.Count), views[0].Version, views);
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
        await SaveAsync(db, $"Die Fassung {version} gibt es bereits.", cancellationToken);
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
        await SaveAsync(db, $"Die Fassung {version} gibt es bereits.", cancellationToken);
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

    /// <summary>Saves and turns a unique-index violation into the message the check above would have given.</summary>
    /// <remarks>
    /// The version check and the insert are two statements, so two editors can both pass the check and collide at
    /// the index. Without this the loser saw a raw database error instead of "gibt es bereits".
    /// </remarks>
    private static async Task SaveAsync(AppDbContext db, string konflikt, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            throw new InvalidOperationException(konflikt, ex);
        }
    }

    private static bool IsDuplicate(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
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
