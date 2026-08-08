using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Financing;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IFinancingConfigService" />
public class FinancingConfigService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache)
    : IFinancingConfigService
{
    private const string SettingKey = "FinanzierungsBudgets";
    private const string CacheKey = "finanzierung:budgets";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>Audit entity type for the config row; SystemSetting is not auditable on its own.</summary>
    public const string AuditType = "FinancingBudgetConfig";

    public async Task<FinancingBudgetConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out FinancingBudgetConfig? cached) && cached is not null)
        {
            return cached;
        }
        var config = await LoadAsync(cancellationToken);
        cache.Set(CacheKey, config, CacheDuration);
        return config;
    }

    public Task<FinancingBudgetConfig> GetEditableAsync(CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken); // always fresh

    private async Task<FinancingBudgetConfig> LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var raw = (await db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == SettingKey, cancellationToken))?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FinancingBudgetConfig.Default();
        }
        try
        {
            var config = JsonSerializer.Deserialize<FinancingBudgetConfig>(raw) ?? FinancingBudgetConfig.Default();
            // a stored config that violates the invariants would hand out impossible budgets → fall back
            Validate(config);
            return config;
        }
        catch (JsonException)
        {
            return FinancingBudgetConfig.Default();
        }
        catch (InvalidOperationException)
        {
            return FinancingBudgetConfig.Default();
        }
    }

    public async Task SaveAsync(FinancingBudgetConfig config, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);
        Validate(config);

        var json = JsonSerializer.Serialize(config);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.SystemSettings.FirstOrDefaultAsync(e => e.Key == SettingKey, cancellationToken);
        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = SettingKey, Value = json });
        }
        else
        {
            row.Value = json;
        }
        // SystemSetting carries no per-field audit, so record the config action explicitly
        db.AuditLogs.Add(ManualAudit.Row(AuditType, "global", AuditAction.Modified, actor,
            ManualAudit.Change("Finanzierungs-Budgets", null, Summarise(config))));
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    /// <summary>Validate config invariants.</summary>
    public static void Validate(FinancingBudgetConfig config)
    {
        foreach (var rank in RankDisplay.All)
        {
            if (!config.Ranks.TryGetValue(FinancingBudgetConfig.RankKey(rank), out var budget))
            {
                continue;
            }
            if (budget.BaseMonthly < 0)
            {
                throw new InvalidOperationException(
                    $"Das Monatsbudget für {RankDisplay.Name(rank)} darf nicht negativ sein.");
            }
            if (budget.CarryOverPercent is < 0 or > 100)
            {
                throw new InvalidOperationException(
                    $"Der Übertrag für {RankDisplay.Name(rank)} muss zwischen 0 und 100 % liegen.");
            }
        }
    }

    private static string Summarise(FinancingBudgetConfig config)
        => string.Join(" · ", RankDisplay.All.Select(r =>
        {
            var budget = config.For(r);
            return $"{RankDisplay.Name(r)}: {Money.Format(budget.BaseMonthly)} / {budget.CarryOverPercent} %";
        }));
}
