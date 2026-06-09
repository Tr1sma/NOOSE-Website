using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IZugriffsLogService" />
public class ZugriffsLogService(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserService currentUserService) : IZugriffsLogService
{
    public async Task LogAnsichtAsync(string entitaetTyp, string entitaetId, CancellationToken cancellationToken = default)
    {
        var user = await currentUserService.GetAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.ZugriffsLogs.Add(new ZugriffsLog
        {
            Zeitpunkt = DateTime.UtcNow,
            AgentId = user.Id,
            AgentName = user.Name,
            EntitaetTyp = entitaetTyp,
            EntitaetId = entitaetId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
