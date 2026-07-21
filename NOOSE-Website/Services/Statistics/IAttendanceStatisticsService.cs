using System.Security.Claims;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Absence and meeting-attendance report; leadership-only, no classification axis.</summary>
public interface IAttendanceStatisticsService
{
    Task<AttendanceReport> GetReportAsync(ClaimsPrincipal actor, int topN = 10, CancellationToken cancellationToken = default);
}
