using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
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
    Task<IReadOnlyList<LlmWeekPoint>> GetWeeklyTrendAsync(string agentId, int weeks = 12, CancellationToken cancellationToken = default);
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

    public async Task<IReadOnlyList<LlmWeekPoint>> GetWeeklyTrendAsync(string agentId, int weeks = 12, CancellationToken cancellationToken = default)
    {
        var periods = await quota.GetPeriodsAsync(agentId, Math.Clamp(weeks, 1, 52), cancellationToken);
        var points = periods
            .Select(p => new LlmWeekPoint(p.Year, p.Week, IsoWeekPeriod.Start(p.Year, p.Week),
                p.Consumed, p.BaseWeekly + p.CarryIn))
            .OrderBy(p => p.StartLocal)
            .ToList();

        // the running week is not closed yet, so it comes from the live status instead of the ledger
        var status = await quota.GetStatusAsync(agentId, cancellationToken);
        if (status.Year > 0 && !points.Any(p => p.Year == status.Year && p.Week == status.Week))
        {
            points.Add(new LlmWeekPoint(status.Year, status.Week,
                IsoWeekPeriod.Start(status.Year, status.Week), status.Consumed, status.Available));
        }
        return points;
    }

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
