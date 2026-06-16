using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;

namespace NOOSE_Website.Infrastructure.Authorization;

/// <summary>Blocks all writes for read-only supervisors; registered first in the interceptor chain.</summary>
public class ReadOnlyBarrierInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    // read-side entities
    private static readonly HashSet<Type> Whitelist =
    [
        typeof(AuditLog),
        typeof(AccessLog),
        typeof(Notification),
    ];

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var user = await currentUserService.GetAsync();
        Require(eventData.Context, user);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        // avoid deadlock
        var user = currentUserService.Get();
        Require(eventData.Context, user);
        return base.SavingChanges(eventData, result);
    }

    private static void Require(DbContext? context, CurrentUserInfo user)
    {
        if (context is null || !user.IsOnlyReader)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }
            if (!Whitelist.Contains(entry.Entity.GetType()))
            {
                throw new UnauthorizedAccessException(
                    "Nur-Lese-Modus: Änderungen sind in der Aufsichtsrolle nicht möglich.");
            }
        }
    }
}
