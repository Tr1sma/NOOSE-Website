using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Reads the NOOSEI request log. Full text is leadership-only, never read-only supervision:
/// agents demonstrably paste real names into prompts, and supervision must never see those.</summary>
public interface ILlmRequestLogService
{
    Task<LlmRequestPage> QueryAsync(LlmRequestFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<LlmRequestDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<LlmRequestFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>An agent's own recent requests, for the "Mein Kontingent" panel.</summary>
    Task<IReadOnlyList<LlmRequestRow>> GetOwnAsync(ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Closed weeks plus the running one, oldest first, for the trend chart.</summary>
    Task<IReadOnlyList<LlmWeekPoint>> GetWeeklyTrendAsync(string agentId, ClaimsPrincipal actor, int weeks = 12, CancellationToken cancellationToken = default);

    /// <summary>Real weekly spend across every agent, oldest first. Carries money, so it is the AI owner's alone.</summary>
    Task<IReadOnlyList<LlmWeekSpend>> GetWeeklySpendAsync(ClaimsPrincipal actor, int weeks = 12, CancellationToken cancellationToken = default);

    /// <summary>Operating figures over the last <paramref name="days" /> days; aggregates only, no prompt text.</summary>
    Task<LlmOperationsReport> GetOperationsAsync(int days, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILlmRequestLogService" />
public sealed class LlmRequestLogService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILlmQuotaService quota) : ILlmRequestLogService
{
    public async Task<LlmRequestPage> QueryAsync(LlmRequestFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var q = db.LlmRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.AgentId))
        {
            q = q.Where(x => x.AgentId == filter.AgentId);
        }
        if (filter.Feature is { } feature)
        {
            q = q.Where(x => x.Feature == feature);
        }
        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            q = q.Where(x => x.Model == filter.Model);
        }
        if (filter.Success is { } success)
        {
            q = q.Where(x => x.Success == success);
        }
        if (filter.AnomalousOnly)
        {
            q = q.Where(x => x.IsAnomalous);
        }
        if (filter.FromUtc is { } from)
        {
            q = q.Where(x => x.CreatedAt >= from);
        }
        if (filter.ToUtc is { } to)
        {
            q = q.Where(x => x.CreatedAt < to);
        }
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            q = q.Where(x => (x.Prompt != null && EF.Functions.Like(x.Prompt, $"%{text}%"))
                || (x.Answer != null && EF.Functions.Like(x.Answer, $"%{text}%")));
        }

        var total = await q.CountAsync(cancellationToken);
        var quotaTotal = await q.SumAsync(x => (long?)x.QuotaTokens, cancellationToken) ?? 0L;
        var costTotal = await q.SumAsync(x => (decimal?)x.CostUsd, cancellationToken) ?? 0m;

        var rows = await q.OrderByDescending(x => x.CreatedAt)
            .Take(LlmRequestFilter.MaxRows)
            .Select(x => new LlmRequestRow(
                x.Id, x.CreatedAt, x.Agent != null ? x.Agent.Codename : null, x.Feature, x.Model, x.Provider,
                x.PromptTokens, x.CompletionTokens, x.CachedTokens, x.QuotaTokens, x.CostUsd,
                x.DurationMs, x.ToolRounds, x.Success, x.ErrorMessage, x.IsAnomalous, x.AnomalyKind))
            .ToListAsync(cancellationToken);

        return new LlmRequestPage(rows, total, quotaTotal, costTotal);
    }

    public async Task<LlmRequestDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.LlmRequests.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                Row = new LlmRequestRow(
                    x.Id, x.CreatedAt, x.Agent != null ? x.Agent.Codename : null, x.Feature, x.Model, x.Provider,
                    x.PromptTokens, x.CompletionTokens, x.CachedTokens, x.QuotaTokens, x.CostUsd,
                    x.DurationMs, x.ToolRounds, x.Success, x.ErrorMessage, x.IsAnomalous, x.AnomalyKind),
                x.Prompt,
                x.Answer,
                x.ContextRefsJson,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new LlmRequestDetail(row.Row, row.Prompt, row.Answer, Refs(row.ContextRefsJson));
    }

    public async Task<LlmRequestFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadershipNoReader(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var agents = await db.Users.AsNoTracking().OnlyListable()
            .OrderBy(a => a.Codename)
            .Select(a => new LlmRequestAgentOption(a.Id, a.Codename ?? a.Id))
            .ToListAsync(cancellationToken);
        // only the models that actually occur; an empty filter list beats a stale hardcoded one
        var models = await db.LlmRequests.AsNoTracking()
            .Where(x => x.Model != null)
            .Select(x => x.Model!)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(cancellationToken);

        return new LlmRequestFilterOptions(agents, models);
    }

    public async Task<IReadOnlyList<LlmRequestRow>> GetOwnAsync(ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrEmpty(agentId))
        {
            return [];
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.LlmRequests.AsNoTracking()
            .Where(x => x.AgentId == agentId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(max, 1, 100))
            .Select(x => new LlmRequestRow(
                x.Id, x.CreatedAt, null, x.Feature, x.Model, x.Provider,
                x.PromptTokens, x.CompletionTokens, x.CachedTokens, x.QuotaTokens, x.CostUsd,
                x.DurationMs, x.ToolRounds, x.Success, x.ErrorMessage, x.IsAnomalous, x.AnomalyKind))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LlmWeekPoint>> GetWeeklyTrendAsync(string agentId, ClaimsPrincipal actor, int weeks = 12, CancellationToken cancellationToken = default)
    {
        var periods = await quota.GetPeriodsAsync(agentId, actor, Math.Clamp(weeks, 1, 52), cancellationToken);
        var points = periods
            .Select(p => new LlmWeekPoint(p.Year, p.Week, IsoWeekPeriod.Start(p.Year, p.Week),
                p.Consumed, p.BaseWeekly + p.CarryIn))
            .OrderBy(p => p.StartLocal)
            .ToList();

        // the running week is not closed yet, so it comes from the live status instead of the ledger
        var status = await quota.GetStatusAsync(agentId, actor, cancellationToken);
        if (status.Year > 0 && !points.Any(p => p.Year == status.Year && p.Week == status.Week))
        {
            points.Add(new LlmWeekPoint(status.Year, status.Week,
                IsoWeekPeriod.Start(status.Year, status.Week), status.Consumed, status.Available));
        }
        return points;
    }

    public async Task<IReadOnlyList<LlmWeekSpend>> GetWeeklySpendAsync(
        ClaimsPrincipal actor, int weeks = 12, CancellationToken cancellationToken = default)
    {
        Permission.RequireAiOwner(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // the budget week is stored on the row: Pomelo cannot translate a UTC-to-local week, and deriving it
        // would pull every request into memory just to sum it
        var rows = await db.LlmRequests.AsNoTracking()
            .GroupBy(x => new { x.BudgetYear, x.BudgetWeek })
            .Select(g => new
            {
                g.Key.BudgetYear,
                g.Key.BudgetWeek,
                Tokens = g.Sum(x => x.QuotaTokens),
                Cost = g.Sum(x => x.CostUsd),
            })
            .OrderByDescending(g => g.BudgetYear).ThenByDescending(g => g.BudgetWeek)
            .Take(Math.Clamp(weeks, 1, 104))
            .ToListAsync(cancellationToken);

        var (year, week) = IsoWeekPeriod.Current();
        return rows
            .Select(r => new LlmWeekSpend(r.BudgetYear, r.BudgetWeek,
                IsoWeekPeriod.Start(r.BudgetYear, r.BudgetWeek), r.Tokens, r.Cost,
                r.BudgetYear == year && r.BudgetWeek == week))
            .OrderBy(r => r.StartLocal)
            .ToList();
    }

    /// <summary>Rows the tool ranking is sampled from. Their names live in a JSON column no index reaches, so the
    /// ranking reads the newest rows rather than every one — and the report says how many that was.</summary>
    private const int ToolSampleRows = 2_000;

    public async Task<LlmOperationsReport> GetOperationsAsync(
        int days, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // same gate as the rest of this service; the figures are aggregates, but the door stays one door
        Permission.RequireLeadershipNoReader(actor);
        var window = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-window);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.LlmRequests.AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .Select(x => new OperationsRow(
                x.Feature, x.Success, x.QuotaTokens, x.PromptTokens, x.CachedTokens, x.DurationMs,
                x.ModelLatencyMs, x.ToolRounds, x.ToolCalls, x.ToolFailures,
                x.FinishReason, x.Withdrawal, x.FailureKind))
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return LlmOperationsReport.Empty with { Days = window };
        }

        var byFeature = rows
            .GroupBy(r => r.Feature)
            .Select(g => new LlmFeatureStat(
                g.Key,
                g.Count(),
                g.Count(r => !r.Success),
                g.Sum(r => r.QuotaTokens),
                Percentile(g.Select(r => r.DurationMs), 0.5),
                Percentile(g.Select(r => r.DurationMs), 0.95),
                // only rows that recorded a model latency; on the rest the difference would be the whole duration
                Percentile(g.Where(r => r.ModelLatencyMs is not null)
                    .Select(r => Math.Max(0, r.DurationMs - r.ModelLatencyMs!.Value)), 0.5)))
            .OrderByDescending(s => s.Total)
            .ToList();

        var rounds = rows
            .GroupBy(r => r.ToolRounds)
            .OrderBy(g => g.Key)
            .Select(g => new LlmCountStat(g.Key == 0 ? "ohne Werkzeuge" : $"{g.Key} Runden", g.Count()))
            .ToList();

        var finishReasons = Distribution(rows
            .Select(r => string.IsNullOrWhiteSpace(r.FinishReason) ? null : r.FinishReason!));
        var withdrawals = Distribution(rows
            .Select(r => r.Withdrawal is { } w ? LlmOperationsDisplay.Name(w) : null));
        var failures = Distribution(rows
            .Where(r => !r.Success)
            .Select(r => LlmOperationsDisplay.Name(r.FailureKind ?? LlmFailureKind.Unknown)));

        return new LlmOperationsReport(
            window,
            rows.Count,
            rows.Count(r => !r.Success),
            rows.Sum(r => r.QuotaTokens),
            rows.Sum(r => (long)r.PromptTokens),
            rows.Sum(r => (long)r.CachedTokens),
            rows.Sum(r => r.ToolCalls ?? 0),
            rows.Sum(r => r.ToolFailures ?? 0),
            Math.Min(rows.Count, ToolSampleRows),
            byFeature,
            await ToolRankingAsync(db, since, cancellationToken),
            rounds,
            finishReasons,
            withdrawals,
            failures);
    }

    /// <summary>How often each tool ran, read out of the stored reference list.</summary>
    private static async Task<IReadOnlyList<LlmCountStat>> ToolRankingAsync(
        AppDbContext db, DateTime since, CancellationToken cancellationToken)
    {
        var payloads = await db.LlmRequests.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.ContextRefsJson != null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(ToolSampleRows)
            .Select(x => x.ContextRefsJson!)
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var payload in payloads)
        {
            foreach (var reference in Refs(payload))
            {
                // only the gateway's own tool entries; a record reference carries a name that has no place in a chart
                if (reference.Kind == "tool" && reference.Name is { Length: > 0 } name)
                {
                    counts[name] = counts.GetValueOrDefault(name) + 1;
                }
            }
        }
        return counts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new LlmCountStat(NooseiToolLabels.Label(kv.Key), kv.Value))
            .ToList();
    }

    private static List<LlmCountStat> Distribution(IEnumerable<string?> labels)
        => labels.Where(l => l is not null)
            .GroupBy(l => l!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => new LlmCountStat(g.Key, g.Count()))
            .ToList();

    /// <summary>Nearest-rank percentile; 0 when nothing was measured.</summary>
    private static int Percentile(IEnumerable<int> values, double share)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }
        var index = (int)Math.Ceiling(share * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private sealed record OperationsRow(
        LlmFeature Feature, bool Success, long QuotaTokens, int PromptTokens, int CachedTokens, int DurationMs,
        int? ModelLatencyMs, int ToolRounds, int? ToolCalls, int? ToolFailures,
        string? FinishReason, LlmToolWithdrawal? Withdrawal, LlmFailureKind? FailureKind);

    private static IReadOnlyList<LlmContextRef> Refs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<LlmContextRef>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
