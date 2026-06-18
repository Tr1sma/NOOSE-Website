using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

public class RecencyService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache) : IRecencyService
{
    private const string CacheKey = "aktualitaet:schwellen";

    /// <summary>Supported record types with default thresholds (warning/stale days).</summary>
    private static readonly RecencyTypeInfo[] Types =
    {
        new(nameof(Person), "Person", 30, 90),
        new(nameof(Faction), "Fraktion", 60, 180),
        new(nameof(PersonGroup), "Personengruppe", 60, 180),
        new(nameof(Party), "Partei", 60, 180),
        new(nameof(Operation), "Operation", 30, 90),
        new(nameof(Taskforce), "Taskforce", 30, 90),
        new(nameof(Case), "Vorgang", 30, 90),
    };

    public IReadOnlyList<RecencyTypeInfo> SupportedTypes => Types;

    public async Task<IReadOnlyDictionary<string, (int WarningDays, int StaleDays)>> GetThresholdsAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, (int, int)>? cached) && cached is not null)
        {
            return cached;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var overrides = await db.RecencyThresholds
            .ToDictionaryAsync(s => s.RecordsType, s => (s.WarningDays, s.StaleDays), cancellationToken);

        var result = Types.ToDictionary(
            t => t.Type,
            t => overrides.TryGetValue(t.Type, out var o) ? o : (t.DefaultWarningDays, t.DefaultStaleDays));

        cache.Set(CacheKey, (IReadOnlyDictionary<string, (int, int)>)result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<(int WarningDays, int StaleDays)> GetThresholdAsync(string recordsType, CancellationToken cancellationToken = default)
    {
        var all = await GetThresholdsAsync(cancellationToken);
        if (all.TryGetValue(recordsType, out var s))
        {
            return s;
        }
        var @default = Types.FirstOrDefault(t => t.Type == recordsType);
        return @default is not null ? (@default.DefaultWarningDays, @default.DefaultStaleDays) : (30, 90);
    }

    public async Task<RecencyLevel> AssessAsync(string recordsType, DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        var (warning, stale) = await GetThresholdAsync(recordsType, cancellationToken);
        return RecencyAssessment.Level(warning, stale, referenceDate, DateTime.UtcNow);
    }

    public async Task SaveAsync(string recordsType, int warningDays, int staleDays, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        if (Types.All(t => t.Type != recordsType))
        {
            throw new InvalidOperationException($"Unbekannter Aktentyp '{recordsType}'.");
        }
        // clamp: stale never before warning
        warningDays = Math.Max(0, warningDays);
        staleDays = Math.Max(warningDays, staleDays);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.RecencyThresholds.FirstOrDefaultAsync(s => s.RecordsType == recordsType, cancellationToken);
        if (exists is null)
        {
            db.RecencyThresholds.Add(new RecencyThreshold
            {
                RecordsType = recordsType,
                WarningDays = warningDays,
                StaleDays = staleDays,
            });
        }
        else
        {
            exists.WarningDays = warningDays;
            exists.StaleDays = staleDays;
        }
        await db.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKey);
    }
}
