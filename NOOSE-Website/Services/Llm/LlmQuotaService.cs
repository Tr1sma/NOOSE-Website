using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Llm;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Weekly NOOSEI token quota: status, lazy period close, charging, and the AI owner's corrections.</summary>
public interface ILlmQuotaService
{
    /// <summary>Quota of one agent for the running ISO week; closes any elapsed weeks first.</summary>
    Task<LlmQuotaStatus> GetStatusAsync(string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Quota of every selectable agent, for the overview.</summary>
    Task<IReadOnlyList<LlmQuotaStatus>> GetAllStatusAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Closed weeks of one agent, newest first — the audit trail of the carry chain.</summary>
    Task<List<LlmQuotaPeriod>> GetPeriodsAsync(string agentId, ClaimsPrincipal actor, int max = 12, CancellationToken cancellationToken = default);

    /// <summary>Manual corrections of one agent, newest first.</summary>
    Task<List<LlmQuotaAdjustment>> GetAdjustmentsAsync(string agentId, ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Pre-flight: throws when the actor may not use NOOSEI or has nothing left this week.</summary>
    Task<LlmQuotaStatus> EnsureAvailableAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Books a finished call against the running week and returns what it cost.</summary>
    Task<LlmQuotaCharge> TryChargeAsync(LlmChargeInput input, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears (null) an agent's individual weekly quota.</summary>
    Task SetOverrideAsync(string agentId, long? amount, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Signed manual correction of the running week (positive tops up, negative deducts).</summary>
    Task TopUpAsync(string agentId, long tokens, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Gives the running week back in full by booking a correction equal to what is consumed.</summary>
    Task ResetAsync(string agentId, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILlmQuotaService" />
public class LlmQuotaService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILlmQuotaConfigService configService) : ILlmQuotaService
{
    public async Task<LlmQuotaStatus> GetStatusAsync(string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireQuotaRead(actor, agentId);
        return await StatusAsync(agentId, cancellationToken);
    }

    /// <summary>Unguarded status, for the pre-flight and the charge — both already know whose quota it is.</summary>
    private async Task<LlmQuotaStatus> StatusAsync(string agentId, CancellationToken cancellationToken)
    {
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Users.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent '{agentId}' nicht gefunden.");
        return await BuildStatusAsync(db, agent, config, cancellationToken);
    }

    public async Task<IReadOnlyList<LlmQuotaStatus>> GetAllStatusAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireQuotaRead(actor);
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agents = await db.Users.AsNoTracking().OnlySelectable()
            .OrderBy(a => a.Codename)
            .ToListAsync(cancellationToken);

        var list = new List<LlmQuotaStatus>(agents.Count);
        foreach (var agent in agents)
        {
            list.Add(await BuildStatusAsync(db, agent, config, cancellationToken));
        }
        return list;
    }

    public async Task<List<LlmQuotaPeriod>> GetPeriodsAsync(string agentId, ClaimsPrincipal actor, int max = 12, CancellationToken cancellationToken = default)
    {
        Permission.RequireQuotaRead(actor, agentId);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.LlmQuotaPeriods.AsNoTracking()
            .Where(p => p.AgentId == agentId)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Week)
            .Take(Math.Max(1, max))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<LlmQuotaAdjustment>> GetAdjustmentsAsync(string agentId, ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default)
    {
        Permission.RequireQuotaRead(actor, agentId);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.LlmQuotaAdjustments.AsNoTracking()
            .Where(a => a.AgentId == agentId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Max(1, max))
            .ToListAsync(cancellationToken);
    }

    public async Task<LlmQuotaStatus> EnsureAvailableAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLlmUse(actor);
        var agentId = actor.GetAgentId();
        if (string.IsNullOrEmpty(agentId))
        {
            throw new UnauthorizedAccessException("NOOSEI steht in dieser Rolle nicht zur Verfügung.");
        }

        var status = await StatusAsync(agentId, cancellationToken);
        if (status.IsBlocked)
        {
            var reset = status.NextResetLocal is { } at ? at.ToString("dd.MM.yyyy HH:mm") : "dem nächsten Wochenwechsel";
            throw new LlmQuotaExceededException(
                $"Dein NOOSEI-Kontingent für {status.PeriodLabel} ist aufgebraucht. Neues Kontingent ab {reset} Uhr.");
        }
        return status;
    }

    public async Task<LlmQuotaCharge> TryChargeAsync(LlmChargeInput input, CancellationToken cancellationToken = default)
    {
        // re-read the clock: a long turn can straddle Monday 00:00, and the row belongs in the week it finished in
        var (year, week) = IsoWeekPeriod.Current();
        var quotaTokens = LlmQuotaMath.FromCost(input.Usage.CostUsd);
        var thresholds = (await configService.GetAsync(cancellationToken)).Anomalies;

        var row = new LlmRequestLog
        {
            AgentId = input.AgentId,
            CreatedAt = DateTime.UtcNow,
            BudgetYear = year,
            BudgetWeek = week,
            Feature = input.Feature,
            Model = input.Model,
            Provider = input.Provider,
            PromptTokens = input.Usage.PromptTokens,
            CompletionTokens = input.Usage.CompletionTokens,
            CachedTokens = input.Usage.CachedPromptTokens,
            ReasoningTokens = input.Usage.ReasoningTokens,
            CostUsd = input.Usage.CostUsd,
            QuotaTokens = quotaTokens,
            DurationMs = input.DurationMs,
            ToolRounds = input.ToolRounds,
            Success = input.Success,
            ErrorMessage = Clip(input.ErrorMessage, 500),
            Prompt = input.Prompt,
            Answer = input.Answer,
            ContextRefsJson = input.ContextRefs is { Count: > 0 } refs ? JsonSerializer.Serialize(refs) : null,
            PromptFingerprint = Fingerprint(input.Prompt),
        };

        var persisted = false;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            // R1 is the one rule judged per row: the row is in hand, and a stored flag turns
            // "only anomalies" into an indexed filter instead of a table scan
            if (input.Success && quotaTokens > 0)
            {
                var (mean, count) = await BaselineAsync(db, input.AgentId, thresholds, cancellationToken);
                if (LlmAnomalyEvaluator.IsCostSpike(quotaTokens, mean, count, thresholds))
                {
                    row.IsAnomalous = true;
                    row.AnomalyKind = LlmAnomalyKind.CostSpike;
                }
            }

            db.LlmRequests.Add(row);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                persisted = true;
            }
            // a failed log row must never swallow the answer the agent is waiting for
            catch (UnauthorizedAccessException) { /* read-only session; nothing to book */ }
            catch (DbUpdateException) { /* best effort */ }
        }

        var status = await StatusAsync(input.AgentId, cancellationToken);
        return new LlmQuotaCharge(quotaTokens, input.Usage.CostUsd, status, row.AnomalyKind, persisted);
    }

    public async Task SetOverrideAsync(string agentId, long? amount, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAiOwner(actor);
        Permission.RequireWriteAccess(actor);
        if (amount is < 0)
        {
            throw new InvalidOperationException("Das Wochenkontingent darf nicht negativ sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Users.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent '{agentId}' nicht gefunden.");
        var previous = agent.LlmQuotaOverride;
        if (previous == amount)
        {
            return;
        }
        agent.LlmQuotaOverride = amount;
        // Agent is not auditable, so log the change against the personnel record explicitly
        db.AuditLogs.Add(ManualAudit.Row(nameof(Agent), agent.Id, AuditAction.Modified, actor,
            ManualAudit.Change("KI-Kontingent",
                previous is null ? "Rang-Standard" : previous.Value.ToString("N0"),
                amount is null ? "Rang-Standard" : amount.Value.ToString("N0"))));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task TopUpAsync(string agentId, long tokens, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAiOwner(actor);
        Permission.RequireWriteAccess(actor);
        if (tokens == 0)
        {
            throw new InvalidOperationException("Die Korrektur muss von null verschieden sein.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Bitte eine Begründung für die Korrektur angeben.");
        }

        var (year, week) = IsoWeekPeriod.Current();
        await AddAdjustmentAsync(agentId, year, week, tokens, reason.Trim(), actor, cancellationToken);
    }

    public async Task ResetAsync(string agentId, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAiOwner(actor);
        Permission.RequireWriteAccess(actor);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Bitte eine Begründung für die Zurücksetzung angeben.");
        }

        var (year, week) = IsoWeekPeriod.Current();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var consumed = await ConsumedAsync(db, agentId, year, week, cancellationToken);
        if (consumed <= 0)
        {
            return;
        }
        // book back exactly what is consumed, so the week starts over while the carry-in stays untouched
        await AddAdjustmentAsync(agentId, year, week, consumed, reason.Trim(), actor, cancellationToken);
    }

    /// <summary>Rolling average this request is judged against: the agent's own recent charges, or — for a
    /// newcomer with no history — everyone's, so a first monster request still trips.</summary>
    private static async Task<(double Mean, int Count)> BaselineAsync(
        AppDbContext db, string agentId, LlmAnomalyThresholds thresholds, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Max(1, thresholds.SpikeBaselineDays));
        var own = await db.LlmRequests.AsNoTracking()
            .Where(r => r.AgentId == agentId && r.CreatedAt >= since && r.Success && r.QuotaTokens > 0)
            .Select(r => r.QuotaTokens)
            .ToListAsync(cancellationToken);
        if (own.Count >= thresholds.SpikeMinBaselineCount)
        {
            return (own.Average(t => (double)t), own.Count);
        }
        if (!thresholds.SpikeUseGlobalFallback)
        {
            return (0, own.Count);
        }

        var global = await db.LlmRequests.AsNoTracking()
            .Where(r => r.CreatedAt >= since && r.Success && r.QuotaTokens > 0)
            .Select(r => r.QuotaTokens)
            .ToListAsync(cancellationToken);
        return global.Count == 0 ? (0, 0) : (global.Average(t => (double)t), global.Count);
    }

    // ---- quota engine ----

    private async Task AddAdjustmentAsync(
        string agentId, int year, int week, long tokens, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Users.AnyAsync(a => a.Id == agentId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Agent '{agentId}' nicht gefunden.");
        }

        db.LlmQuotaAdjustments.Add(new LlmQuotaAdjustment
        {
            AgentId = agentId,
            Year = year,
            Week = week,
            Tokens = tokens,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
            CreatedById = actor.GetAgentId(),
            CreatedByName = actor.GetCodename(),
        });
        db.AuditLogs.Add(ManualAudit.Row(nameof(Agent), agentId, AuditAction.Modified, actor,
            ManualAudit.Change("KI-Kontingent-Korrektur", null,
                $"{(tokens > 0 ? "+" : string.Empty)}{tokens:N0} Token · {reason}")));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<LlmQuotaStatus> BuildStatusAsync(
        AppDbContext db, Agent agent, LlmQuotaConfig config, CancellationToken cancellationToken)
    {
        var (year, week) = IsoWeekPeriod.Current();
        var rules = config.For(agent.Rank);
        var baseWeekly = agent.LlmQuotaOverride ?? rules.BaseWeekly;
        var carryIn = await CloseElapsedAsync(db, agent, baseWeekly, rules.CarryOverPercent, year, week, cancellationToken);
        var consumed = await ConsumedAsync(db, agent.Id, year, week, cancellationToken);
        return new LlmQuotaStatus(agent.Id, agent.Codename, agent.Rank, year, week,
            baseWeekly, carryIn, consumed, rules.CarryOverPercent, agent.LlmQuotaOverride is not null);
    }

    /// <summary>Net consumption of one week: what the requests charged, minus what was corrected back.</summary>
    private static async Task<long> ConsumedAsync(AppDbContext db, string agentId, int year, int week, CancellationToken cancellationToken)
    {
        var charged = await db.LlmRequests.AsNoTracking()
            .Where(r => r.AgentId == agentId && r.BudgetYear == year && r.BudgetWeek == week)
            .SumAsync(r => (long?)r.QuotaTokens, cancellationToken) ?? 0L;
        var corrected = await db.LlmQuotaAdjustments.AsNoTracking()
            .Where(a => a.AgentId == agentId && a.Year == year && a.Week == week)
            .SumAsync(a => (long?)a.Tokens, cancellationToken) ?? 0L;
        return charged - corrected;
    }

    /// <summary>Writes a period row for every elapsed week and returns the carry-over the running week may use.</summary>
    private static async Task<long> CloseElapsedAsync(
        AppDbContext db, Agent agent, long baseWeekly, int carryPercent, int currentYear, int currentWeek, CancellationToken cancellationToken)
    {
        var latest = await db.LlmQuotaPeriods.AsNoTracking()
            .Where(p => p.AgentId == agent.Id)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Week)
            .Select(p => new { p.Year, p.Week, p.CarryOut })
            .FirstOrDefaultAsync(cancellationToken);

        var first = await FirstChargedWeekAsync(db, agent.Id, cancellationToken);

        var consumedByWeek = await ConsumedByWeekAsync(db, agent.Id, cancellationToken);

        var plan = LlmQuotaLedger.Backfill(
            latest is null ? null : new LlmQuotaLedger.WeekKey(latest.Year, latest.Week),
            latest?.CarryOut ?? 0L,
            first,
            consumedByWeek,
            baseWeekly,
            carryPercent,
            currentYear,
            currentWeek);

        if (plan.Outcome == BackfillOutcome.ReadPredecessor)
        {
            // the running week (or later) is already closed — anomaly; read the direct predecessor instead
            var (priorYear, priorWeek) = IsoWeekPeriod.Previous(currentYear, currentWeek);
            var stored = await db.LlmQuotaPeriods.AsNoTracking()
                .Where(p => p.AgentId == agent.Id && p.Year == priorYear && p.Week == priorWeek)
                .Select(p => (long?)p.CarryOut)
                .FirstOrDefaultAsync(cancellationToken) ?? 0L;
            return LlmQuotaMath.ClampCarryIn(stored, baseWeekly, carryPercent);
        }

        var persist = true;
        foreach (var draft in plan.ToClose)
        {
            if (!persist)
            {
                continue;
            }
            persist = await TryCloseAsync(db, agent, draft, cancellationToken);
        }
        return plan.CarryIn;
    }

    private static async Task<LlmQuotaLedger.WeekKey?> FirstChargedWeekAsync(AppDbContext db, string agentId, CancellationToken cancellationToken)
    {
        // start at the earliest week that actually carries a charge or a correction; weeks before that had
        // nothing to spend, so they must not manufacture carry-over out of an untouched quota
        var firstRequest = await db.LlmRequests.AsNoTracking()
            .Where(r => r.AgentId == agentId)
            .OrderBy(r => r.BudgetYear).ThenBy(r => r.BudgetWeek)
            .Select(r => new { r.BudgetYear, r.BudgetWeek })
            .FirstOrDefaultAsync(cancellationToken);
        var firstAdjustment = await db.LlmQuotaAdjustments.AsNoTracking()
            .Where(a => a.AgentId == agentId)
            .OrderBy(a => a.Year).ThenBy(a => a.Week)
            .Select(a => new { BudgetYear = a.Year, BudgetWeek = a.Week })
            .FirstOrDefaultAsync(cancellationToken);

        if (firstRequest is null && firstAdjustment is null)
        {
            return null;
        }
        if (firstRequest is null)
        {
            return new LlmQuotaLedger.WeekKey(firstAdjustment!.BudgetYear, firstAdjustment.BudgetWeek);
        }
        if (firstAdjustment is null)
        {
            return new LlmQuotaLedger.WeekKey(firstRequest.BudgetYear, firstRequest.BudgetWeek);
        }
        return IsoWeekPeriod.IsBefore(firstAdjustment.BudgetYear, firstAdjustment.BudgetWeek, firstRequest.BudgetYear, firstRequest.BudgetWeek)
            ? new LlmQuotaLedger.WeekKey(firstAdjustment.BudgetYear, firstAdjustment.BudgetWeek)
            : new LlmQuotaLedger.WeekKey(firstRequest.BudgetYear, firstRequest.BudgetWeek);
    }

    /// <summary>Net consumption per week in one grouped pass, so the backfill never queries per week.</summary>
    private static async Task<Dictionary<LlmQuotaLedger.WeekKey, long>> ConsumedByWeekAsync(
        AppDbContext db, string agentId, CancellationToken cancellationToken)
    {
        var map = new Dictionary<LlmQuotaLedger.WeekKey, long>();

        var charged = await db.LlmRequests.AsNoTracking()
            .Where(r => r.AgentId == agentId)
            .GroupBy(r => new { r.BudgetYear, r.BudgetWeek })
            .Select(g => new { g.Key.BudgetYear, g.Key.BudgetWeek, Sum = g.Sum(x => x.QuotaTokens) })
            .ToListAsync(cancellationToken);
        foreach (var row in charged)
        {
            map[new LlmQuotaLedger.WeekKey(row.BudgetYear, row.BudgetWeek)] = row.Sum;
        }

        var corrected = await db.LlmQuotaAdjustments.AsNoTracking()
            .Where(a => a.AgentId == agentId)
            .GroupBy(a => new { a.Year, a.Week })
            .Select(g => new { g.Key.Year, g.Key.Week, Sum = g.Sum(x => x.Tokens) })
            .ToListAsync(cancellationToken);
        foreach (var row in corrected)
        {
            var key = new LlmQuotaLedger.WeekKey(row.Year, row.Week);
            map[key] = map.GetValueOrDefault(key) - row.Sum;
        }

        return map;
    }

    private static async Task<bool> TryCloseAsync(
        AppDbContext db, Agent agent, LlmQuotaLedger.PeriodDraft draft, CancellationToken cancellationToken)
    {
        var row = new LlmQuotaPeriod
        {
            AgentId = agent.Id,
            Year = draft.Year,
            Week = draft.Week,
            BaseWeekly = draft.BaseWeekly,
            CarryIn = draft.CarryIn,
            Consumed = draft.Consumed,
            CarryOut = draft.CarryOut,
            CarryPercent = draft.CarryPercent,
            RankAtClose = agent.Rank,
            ClosedAt = DateTime.UtcNow,
        };
        db.LlmQuotaPeriods.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // a pure read by read-only supervision or a demo visitor triggered this; compute without persisting
            db.Entry(row).State = EntityState.Detached;
            return false;
        }
        catch (DbUpdateException)
        {
            // the unique index on (Agent, Jahr, Woche) caught a concurrent close; its numbers stand
            db.Entry(row).State = EntityState.Detached;
            return true;
        }
    }

    // ---- helpers ----

    private static string? Clip(string? text, int max)
        => string.IsNullOrWhiteSpace(text) ? null : text.Length <= max ? text : text[..max];

    /// <summary>Normalised prompt hash; the first, cheap pass of the near-identical-prompt rule.</summary>
    public static string? Fingerprint(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }
        var sb = new StringBuilder(Math.Min(prompt.Length, 512));
        var space = false;
        foreach (var c in prompt)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                space = false;
            }
            else if (!space)
            {
                sb.Append(' ');
                space = true;
            }
            if (sb.Length >= 512)
            {
                break;
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString().Trim())));
    }
}

/// <summary>The actor is out of NOOSEI tokens for this week. Typed so the UI can catch it specifically.</summary>
public sealed class LlmQuotaExceededException(string message) : InvalidOperationException(message);
