using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;

namespace NOOSE_Website.Services;

public class AccessLogService(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserService currentUserService) : IAccessLogService
{
    public async Task LogViewAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        var user = await currentUserService.GetAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.AccessLogs.Add(new AccessLog
        {
            Timestamp = DateTime.UtcNow,
            AgentId = user.Id,
            AgentName = user.Name,
            EntityType = entityType,
            EntityId = entityId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTime?> PreviousVisitAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        var user = await currentUserService.GetAsync();
        // every demo visitor shares one account, so its last visit is a stranger's
        if (string.IsNullOrEmpty(user.Id) || user.IsDemo)
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var views = await db.AccessLogs.AsNoTracking()
            .Where(a => a.AgentId == user.Id && a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => a.Timestamp)
            .Take(RecordVisits.LookBack)
            .ToListAsync(cancellationToken);
        return RecordVisits.PreviousSessionEnd(views, DateTime.UtcNow);
    }
}
