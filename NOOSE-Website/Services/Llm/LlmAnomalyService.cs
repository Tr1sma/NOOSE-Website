using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Live NOOSEI misuse flags for the admin overview.</summary>
public interface ILlmAnomalyService
{
    Task<IReadOnlyList<LlmAnomalyFlag>> GetFlagsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILlmAnomalyService" />
public sealed class LlmAnomalyService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILlmQuotaConfigService configService,
    ILlmQuotaService quota,
    IMemoryCache cache) : ILlmAnomalyService
{
    private const string CacheKey = "ki:anomalien";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<LlmAnomalyFlag>> GetFlagsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // the panel names agents and their behaviour, so it follows the counter-intelligence gate
        Permission.RequireLeadershipNoReader(actor);
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<LlmAnomalyFlag>? cached) && cached is not null)
        {
            return cached;
        }

        var config = await configService.GetAsync(cancellationToken);
        var statuses = await quota.GetAllStatusAsync(actor, cancellationToken);
        var (year, week) = IsoWeekPeriod.Current();
        var weekStartUtc = IsoWeekPeriod.Start(year, week).ToUniversalTime();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.LlmRequests.AsNoTracking()
            .Where(r => r.CreatedAt >= weekStartUtc && r.Success)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.AgentId, r.CreatedAt, r.QuotaTokens, r.PromptFingerprint, r.Prompt })
            .ToListAsync(cancellationToken);

        var usage = rows
            .Select(r => new LlmUsageRow(r.AgentId, null, r.CreatedAt.ToLocalTime(), r.QuotaTokens,
                r.PromptFingerprint, r.Prompt))
            .ToList();

        var trailing = await TrailingMeansAsync(db, config.Anomalies.OutlierTrailingWeeks,
            config.Anomalies.OutlierMinWeeks, year, week, cancellationToken);

        // the rank baseline is derived inside the evaluator, per agent and without them in it
        var flags = LlmAnomalyEvaluator.Evaluate(usage, statuses, trailing, config.Anomalies, DateTime.Now);
        cache.Set(CacheKey, flags, CacheDuration);
        return flags;
    }

    /// <summary>Mean consumption of an agent's recent closed weeks; agents without enough history are left out.</summary>
    private static async Task<Dictionary<string, double>> TrailingMeansAsync(
        AppDbContext db, int trailingWeeks, int minWeeks, int currentYear, int currentWeek, CancellationToken cancellationToken)
    {
        var oldest = (currentYear, currentWeek);
        for (var i = 0; i < Math.Max(1, trailingWeeks); i++)
        {
            oldest = IsoWeekPeriod.Previous(oldest.Item1, oldest.Item2);
        }

        var periods = await db.LlmQuotaPeriods.AsNoTracking()
            .Where(p => p.Year > oldest.Item1 || (p.Year == oldest.Item1 && p.Week >= oldest.Item2))
            .Select(p => new { p.AgentId, p.Consumed })
            .ToListAsync(cancellationToken);

        return periods
            .GroupBy(p => p.AgentId)
            .Where(g => g.Count() >= Math.Max(1, minWeeks))
            .ToDictionary(g => g.Key, g => g.Average(p => (double)p.Consumed));
    }
}
