using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicModuleService" />
public class PublicModuleService(
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache) : IPublicModuleService
{
    private const string CacheKey = "OeffentlicheModule";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>Audit entity type naming the kill-switch action; the raw SystemSetting row is logged separately.</summary>
    public const string AuditType = "PublicArea";

    public async Task<PublicModuleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out PublicModuleSnapshot? snapshot) && snapshot is not null)
        {
            return snapshot;
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.OeffentlicheModule
                .AsNoTracking()
                .ToDictionaryAsync(m => m.Key, m => m, StringComparer.Ordinal, cancellationToken);
            var kill = await db.SystemSettings
                .Where(s => s.Key == SystemSettingKeys.PublicAreaKillSwitch)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);

            snapshot = new PublicModuleSnapshot(
                KillSwitchActive: string.Equals(kill, "true", StringComparison.OrdinalIgnoreCase),
                Modules: Merge(rows));
        }
        catch (Exception)
        {
            // unreachable database falls back to the catalog defaults, which is what a fresh install shows: almost
            // everything off. A saved "on" is lost for the cache window, a saved "off" is never overridden.
            return CatalogDefault();
        }

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
        => (await GetAsync(cancellationToken)).IsEnabled(key);

    public async Task RequireEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(cancellationToken);
        if (snapshot.KillSwitchActive)
        {
            throw new InvalidOperationException("Der öffentliche Bereich ist derzeit vollständig abgeschaltet.");
        }
        if (!snapshot.IsEnabled(key))
        {
            var label = snapshot.Find(key)?.Label ?? key;
            throw new InvalidOperationException($"Das Modul „{label}“ ist derzeit abgeschaltet.");
        }
    }

    public async Task<string> OfflineTextAsync(string key, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(cancellationToken);
        var text = snapshot.Find(key)?.OfflineText;
        return string.IsNullOrWhiteSpace(text) ? "Dieser Bereich ist derzeit nicht verfügbar." : text;
    }

    public async Task<IReadOnlyList<PublicModuleState>> NavEntriesAsync(CancellationToken cancellationToken = default)
        => (await GetAsync(cancellationToken)).NavEntries();

    public async Task SaveAsync(IEnumerable<PublicModuleInput> rows, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAdmin(actor);

        var inputs = rows.ToList();
        var unknown = inputs.Select(i => i.Key).FirstOrDefault(k => PublicModules.Find(k) is null);
        if (unknown is not null)
        {
            throw new InvalidOperationException($"Unbekanntes Modul „{unknown}“.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.OeffentlicheModule.ToDictionaryAsync(m => m.Key, m => m, StringComparer.Ordinal, cancellationToken);

        foreach (var input in inputs)
        {
            if (!existing.TryGetValue(input.Key, out var row))
            {
                row = new OeffentlichesModul { Key = input.Key };
                db.OeffentlicheModule.Add(row);
                // remember it: a repeated key in the same call would otherwise add a second row and hit the
                // unique index instead of simply winning
                existing[input.Key] = row;
            }

            row.IsEnabled = input.IsEnabled;
            row.OfflineText = Empty(input.OfflineText);
            row.SortOrder = Math.Clamp(input.SortOrder, 0, 9999);
            row.LabelOverride = Cut(Empty(input.LabelOverride), 64);
            // an unknown icon name is dropped, not stored: the override is an allowlist choice, never free text
            row.IconOverride = PublicModules.IsKnownIcon(input.IconOverride) ? input.IconOverride!.Trim() : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task KillSwitchSetAsync(bool active, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAdmin(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == SystemSettingKeys.PublicAreaKillSwitch, cancellationToken);
        var previous = string.Equals(row?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var value = active ? "true" : "false";

        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.PublicAreaKillSwitch, Value = value });
        }
        else
        {
            row.Value = value;
        }

        // SystemSetting is IAuditable, so the interceptor already logs the raw key/value change. This row names the
        // action instead, the same way the other config services do — two rows, one of which is readable.
        db.AuditLogs.Add(ManualAudit.Row(AuditType, "kill-switch", AuditAction.Modified, actor,
            ManualAudit.Change("Öffentlicher Bereich abgeschaltet", previous, active)));

        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    private static IReadOnlyList<PublicModuleState> Merge(IReadOnlyDictionary<string, OeffentlichesModul> rows)
        => PublicModules.All
            .Select(definition =>
            {
                var row = rows.GetValueOrDefault(definition.Key);
                return new PublicModuleState(
                    Key: definition.Key,
                    Label: Empty(row?.LabelOverride) ?? definition.Label,
                    Description: definition.Description,
                    Icon: PublicModules.IconFor(row?.IconOverride, definition.Icon),
                    NavRoute: definition.NavRoute,
                    Group: definition.Group,
                    SortOrder: row?.SortOrder ?? definition.SortOrder,
                    IsEnabled: row?.IsEnabled ?? definition.DefaultEnabled,
                    OfflineText: Empty(row?.OfflineText) ?? definition.DefaultOfflineText,
                    Available: definition.Available,
                    LabelOverride: Empty(row?.LabelOverride),
                    IconOverride: Empty(row?.IconOverride),
                    OfflineTextOverride: Empty(row?.OfflineText));
            })
            .OrderBy(m => m.Group)
            .ThenBy(m => m.SortOrder)
            .ToList();

    private static PublicModuleSnapshot CatalogDefault()
        => new(KillSwitchActive: false, Modules: Merge(new Dictionary<string, OeffentlichesModul>(StringComparer.Ordinal)));

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Cut(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
