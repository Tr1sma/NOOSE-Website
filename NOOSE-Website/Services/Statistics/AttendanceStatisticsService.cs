using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="IAttendanceStatisticsService" />
public class AttendanceStatisticsService(
    IDbContextFactory<AppDbContext> dbFactory,
    ISystemSettingService settings) : IAttendanceStatisticsService
{
    private const int TimeSeriesMonths = 12;

    public async Task<AttendanceReport> GetReportAsync(ClaimsPrincipal actor, int topN = 10,
        CancellationToken cancellationToken = default)
    {
        // whole-service gate: the result does not vary per viewer, so the actor guards access, not filtering.
        // Read-only supervision must pass, since the page policy (LeadershipPage) admits it.
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var configuration = await settings.GetAsync(cancellationToken);
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(
            configuration.MeetingWindowSize, configuration.MeetingAnomalyYellow, configuration.MeetingAnomalyRed);

        // AttendanceClosedAt is the freeze signal; Status can be edited independently, so do not gate on it
        var windowMeetingIds = await db.Meetings.AsNoTracking()
            .Where(m => m.AttendanceClosedAt != null)
            .OrderByDescending(m => m.Start)
            .Select(m => m.Id)
            .Take(window)
            .ToListAsync(cancellationToken);

        var windowRows = windowMeetingIds.Count == 0
            ? new List<AttendanceRaw>()
            : await db.MeetingAttendances.AsNoTracking()
                .Where(t => windowMeetingIds.Contains(t.MeetingId))
                .Select(t => new AttendanceRaw(t.AgentId, t.Status))
                .ToListAsync(cancellationToken);

        var agents = await db.Users.AsNoTracking().OnlySelectable()
            .Select(u => new { u.Id, u.Codename })
            .ToListAsync(cancellationToken);

        var byAgent = windowRows.GroupBy(r => r.AgentId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var all = new List<AttendanceAgentRow>(agents.Count);
        foreach (var agent in agents)
        {
            byAgent.TryGetValue(agent.Id, out var rows);
            rows ??= new List<AttendanceRaw>();

            var present = rows.Count(r => r.Status == MeetingAttendanceStatus.Present);
            var excused = rows.Count(r => r.Status == MeetingAttendanceStatus.SignedOff);
            var missing = rows.Count(r => r.Status == MeetingAttendanceStatus.Missing);

            all.Add(new AttendanceAgentRow(
                agent.Id, agent.Codename, $"/personal/{agent.Id}",
                rows.Count, present, excused, missing,
                AttendanceAnomalyLogic.From(rows.Count, missing, window, yellow, red)));
        }

        var anomalies = all
            .Where(a => a.Level is AttendanceAnomalyLevel.Yellow or AttendanceAnomalyLevel.Red)
            .OrderByDescending(a => a.Level)
            .ThenByDescending(a => a.Missing)
            .ThenBy(a => a.Codename)
            .Take(topN)
            .ToList();

        var attendanceDistribution = new[]
            {
                MeetingAttendanceStatus.Present,
                MeetingAttendanceStatus.SignedOff,
                MeetingAttendanceStatus.Missing,
            }
            .Select(s => new DistributionSegment(
                MeetingAttendanceStatusDisplay.Name(s), windowRows.Count(r => r.Status == s)))
            .ToList();

        var categoryCount = (await db.Absences.AsNoTracking()
                .GroupBy(a => a.Category)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Value, x => x.Count);
        var absencesByCategory = AbsenceCategoryDisplay.All
            .Select(c => new DistributionSegment(AbsenceCategoryDisplay.Name(c), categoryCount.GetValueOrDefault(c)))
            .ToList();

        var openAcknowledgement = await db.Absences.AsNoTracking()
            .CountAsync(a => a.AcknowledgedAt == null, cancellationToken);

        var (months, absencesPerMonth, missingPerMonth) = await TimeSeriesAsync(db, cancellationToken);

        return new AttendanceReport(
            window, yellow, red,
            windowMeetingIds.Count,
            openAcknowledgement,
            attendanceDistribution,
            absencesByCategory,
            months,
            absencesPerMonth,
            missingPerMonth,
            anomalies,
            all.OrderBy(a => a.Codename).ToList());
    }

    /// <summary>Twelve rolling months of filed absences and unexcused misses.</summary>
    private static async Task<(List<StatisticsMonth> Months, List<int> Absences, List<int> Missing)> TimeSeriesAsync(
        AppDbContext db, CancellationToken cancellationToken)
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        // the buckets are local months, so seed them from the local clock, not from UTC
        var nowLocal = MeetingTime.Local(DateTime.UtcNow);
        var firstLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1)
            .AddMonths(-(TimeSeriesMonths - 1));
        var fromDay = DateOnly.FromDateTime(firstLocal);
        var fromUtc = MeetingTime.ToUtc(firstLocal);

        var absenceStarts = await db.Absences.AsNoTracking()
            .Where(a => a.FromDate >= fromDay)
            .Select(a => a.FromDate)
            .ToListAsync(cancellationToken);

        var misses = await db.MeetingAttendances.AsNoTracking()
            .Where(t => t.Status == MeetingAttendanceStatus.Missing
                     && t.Meeting!.AttendanceClosedAt != null
                     && t.Meeting.Start >= fromUtc)
            .Select(t => t.Meeting!.Start)
            .ToListAsync(cancellationToken);

        // bucket in memory: the local-month mapping is not translatable
        var missMonths = misses.Select(MeetingTime.Local).ToList();

        var months = new List<StatisticsMonth>(TimeSeriesMonths);
        var absencesPerMonth = new List<int>(TimeSeriesMonths);
        var missingPerMonth = new List<int>(TimeSeriesMonths);

        for (var i = 0; i < TimeSeriesMonths; i++)
        {
            var cursor = firstLocal.AddMonths(i);
            months.Add(new StatisticsMonth(cursor.Year, cursor.Month,
                cursor.ToString("MMM yy", german), 0, 0));
            absencesPerMonth.Add(absenceStarts.Count(d => d.Year == cursor.Year && d.Month == cursor.Month));
            missingPerMonth.Add(missMonths.Count(d => d.Year == cursor.Year && d.Month == cursor.Month));
        }

        return (months, absencesPerMonth, missingPerMonth);
    }

    private readonly record struct AttendanceRaw(string AgentId, MeetingAttendanceStatus Status);
}
