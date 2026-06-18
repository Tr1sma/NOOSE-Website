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
}
