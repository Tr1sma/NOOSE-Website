using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="ISituationReportService" />
public class SituationReportService(
    IDbContextFactory<AppDbContext> dbFactory,
    IStatisticsService statistics,
    INotificationService notifications,
    ILogger<SituationReportService> logger) : ISituationReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    public async Task<bool> GenerateDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var previousMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var bulletin = await GenerateMonthAsync(previousMonth.Year, previousMonth.Month, replaceExisting: false,
            triggerId: null, cancellationToken);
        return bulletin is not null;
    }

    public async Task<SituationReport?> GenerateMonthAsync(int year, int month, bool replaceExisting, string? triggerId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.SituationReports
            .Where(l => l.Year == year && l.Month == month)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            if (!replaceExisting)
            {
                return null;
            }
            foreach (var alt in existing)
            {
                alt.IsDeleted = true;
                alt.DeletedAt = DateTime.UtcNow;
                alt.DeletedById = triggerId;
            }
        }

        var report = await statistics.GetReportAsync(isLeadership: true, meId: null, cancellationToken: cancellationToken);
        var title = $"Lagebericht {new DateTime(year, month, 1).ToString("MMMM yyyy", DeDe)}";

        var bulletin = new SituationReport
        {
            Year = year,
            Month = month,
            Title = title,
            SnapshotJson = JsonSerializer.Serialize(report, JsonOptions),
            CreatedById = triggerId,
        };
        db.SituationReports.Add(bulletin);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyLeadershipAsync(db, bulletin, title, triggerId, cancellationToken);
        return bulletin;
    }

    public async Task<List<SituationReportHead>> GetArchiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.SituationReports
            .OrderByDescending(l => l.Year).ThenByDescending(l => l.Month).ThenByDescending(l => l.CreatedAt)
            .Select(l => new { l.Id, l.Year, l.Month, l.Title, l.CreatedAt, l.CreatedById })
            .ToListAsync(cancellationToken);

        var creatorIds = rows.Where(r => !string.IsNullOrEmpty(r.CreatedById))
            .Select(r => r.CreatedById!).Distinct().ToList();
        var names = creatorIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.Where(u => creatorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToDictionaryAsync(u => u.Id, u => u.Codename, cancellationToken);

        return rows.Select(r => new SituationReportHead(r.Id, r.Year, r.Month, r.Title, r.CreatedAt,
            string.IsNullOrEmpty(r.CreatedById) ? null : names.GetValueOrDefault(r.CreatedById))).ToList();
    }

    public async Task<SituationReportDisplay?> GetDisplayAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bulletin = await db.SituationReports.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (bulletin is null)
        {
            return null;
        }

        StatisticsReport? report;
        try
        {
            report = JsonSerializer.Deserialize<StatisticsReport>(bulletin.SnapshotJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Lagebericht {Id} hat einen unlesbaren Snapshot.", id);
            return null;
        }
        if (report is null)
        {
            return null;
        }

        string? generatedBy = null;
        if (!string.IsNullOrEmpty(bulletin.CreatedById))
        {
            generatedBy = await db.Users.Where(u => u.Id == bulletin.CreatedById)
                .Select(u => u.Codename).FirstOrDefaultAsync(cancellationToken);
        }

        return new SituationReportDisplay(bulletin.Id, bulletin.Title, bulletin.CreatedAt, generatedBy, report);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bulletin = await db.SituationReports.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (bulletin is null)
        {
            return;
        }
        bulletin.IsDeleted = true;
        bulletin.DeletedAt = DateTime.UtcNow;
        bulletin.DeletedById = actor.GetAgentId();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyLeadershipAsync(AppDbContext db, SituationReport bulletin, string title,
        string? triggerId, CancellationToken cancellationToken)
    {
        try
        {
            var leadershipIds = await db.Users
                .Where(u => u.Status == AgentStatus.Active && u.Rank != null
                    && u.Rank >= Rank.SupervisorySpecialAgent)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            await notifications.NotifyManyAsync(leadershipIds, NotificationType.SituationReport,
                $"Neuer {title}", $"/lageberichte/{bulletin.Id}", triggerId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Benachrichtigung über den neuen Lagebericht {Id} fehlgeschlagen.", bulletin.Id);
        }
    }
}
