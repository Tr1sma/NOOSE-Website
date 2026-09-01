using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicSituationService" />
public class PublicSituationService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IMemoryCache cache) : IPublicSituationService
{
    private const string CacheKey = "OeffentlicheGefahrenlage";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>Audit entity type naming the action; the raw SystemSetting rows are logged separately.</summary>
    public const string AuditType = "PublicSituation";

    private static readonly string[] Keys =
    [
        SystemSettingKeys.PublicSituationLevel,
        SystemSettingKeys.PublicSituationNote,
        SystemSettingKeys.PublicSituationSince,
        SystemSettingKeys.PublicSituationPrevious,
    ];

    /// <summary>Cache holder, so "nothing is being said" is a cached answer rather than a repeated query.</summary>
    private sealed record Entry(PublicSituationState? State);

    public async Task<PublicSituationState?> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        // the module switch is read outside the content cache: caching "module is off" would keep the page dark for a
        // whole cache window after someone turns it back on
        if (!await modules.IsEnabledAsync(PublicModules.HazardLevel, cancellationToken))
        {
            return null;
        }
        return await LoadAsync(cancellationToken);
    }

    public async Task<PublicSituationState?> GetForEditAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);
        // no module gate: the panel edits what is stored, and the switch next door decides whether it is outside
        return await LoadAsync(cancellationToken);
    }

    public async Task SetAsync(PublicSituationInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicSituationWrite(actor);

        // deliberately no RequireEnabledAsync, unlike every other publish path. There is no draft here, so gating the
        // write on the module would force the first level ever set to go live before anyone can write it — and
        // switching the module off is how a level is taken back, which the gate would then make impossible.
        var note = (input.Note ?? string.Empty).Trim();
        if (note.Length > SituationRules.MaxNote)
        {
            note = note[..SituationRules.MaxNote];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.SystemSettings
            .Where(s => Keys.Contains(s.Key))
            .ToListAsync(cancellationToken);

        var storedLevel = PublicSituationLevelDisplay.Parse(Value(rows, SystemSettingKeys.PublicSituationLevel));
        var storedNote = Value(rows, SystemSettingKeys.PublicSituationNote) ?? string.Empty;
        var levelChanged = storedLevel != input.Level;
        var noteChanged = !string.Equals(storedNote, note, StringComparison.Ordinal);

        if (!levelChanged && !noteChanged)
        {
            // nothing said differently, so nothing to log and nothing to invalidate
            return;
        }

        Set(db, rows, SystemSettingKeys.PublicSituationLevel, PublicSituationLevelDisplay.Name(input.Level));
        Set(db, rows, SystemSettingKeys.PublicSituationNote, note);

        var changes = new Dictionary<string, object?[]>(StringComparer.Ordinal);
        if (noteChanged)
        {
            changes["Einschätzung"] = [storedNote, note];
        }
        if (levelChanged)
        {
            var previousName = storedLevel is { } previous ? PublicSituationLevelDisplay.Name(previous) : null;
            changes["Gefahrenlage"] = [previousName, PublicSituationLevelDisplay.Name(input.Level)];

            // only a change of level moves the date and records the predecessor. The page shows this date as "seit",
            // so correcting a typo in the assessment must not claim the situation changed today.
            Set(db, rows, SystemSettingKeys.PublicSituationSince,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            Set(db, rows, SystemSettingKeys.PublicSituationPrevious, previousName);
        }

        // SystemSetting is IAuditable, so the interceptor already logs the raw key/value rows. This one names the
        // action instead, the way the other config services do — two rows, one of which is readable.
        db.AuditLogs.Add(ManualAudit.Row(AuditType, "gefahrenlage", AuditAction.Modified, actor, changes));

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    private async Task<PublicSituationState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out Entry? cached) && cached is not null)
        {
            return cached.State;
        }

        Entry entry;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.SystemSettings
                .AsNoTracking()
                .Where(s => Keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal, cancellationToken);
            entry = new Entry(Build(rows));
        }
        catch (Exception)
        {
            // when the agency cannot say, it says nothing: an unreachable database must not answer with a level
            return null;
        }

        cache.Set(CacheKey, entry, CacheDuration);
        return entry.State;
    }

    private static PublicSituationState? Build(IReadOnlyDictionary<string, string?> rows)
    {
        var level = PublicSituationLevelDisplay.Parse(rows.GetValueOrDefault(SystemSettingKeys.PublicSituationLevel));
        if (level is null)
        {
            // never set, or a stray value in a hand-editable row: silence, not a default level
            return null;
        }

        return new PublicSituationState(
            level.Value,
            rows.GetValueOrDefault(SystemSettingKeys.PublicSituationNote) ?? string.Empty,
            ParseTime(rows.GetValueOrDefault(SystemSettingKeys.PublicSituationSince)),
            PublicSituationLevelDisplay.Parse(rows.GetValueOrDefault(SystemSettingKeys.PublicSituationPrevious)));
    }

    private static DateTime? ParseTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static string? Value(List<SystemSetting> rows, string key)
        => rows.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal))?.Value;

    private static void Set(AppDbContext db, List<SystemSetting> rows, string key, string? value)
    {
        var row = rows.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));
        if (row is null)
        {
            row = new SystemSetting { Key = key, Value = value };
            rows.Add(row);
            db.SystemSettings.Add(row);
        }
        else
        {
            row.Value = value;
        }
    }

    /// <summary>The one save path of this state: nothing writes it without dropping the snapshot.</summary>
    /// <remarks>A file scan holds this shape (<c>PublicSituationCacheDisciplineTests</c>).</remarks>
    private async Task SaveAndInvalidateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }
}
