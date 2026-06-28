using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;

namespace NOOSE_Website.Infrastructure.Authorization;

/// <summary>Blocks all writes for read-only supervisors and partners; registered first in the interceptor chain.</summary>
public class ReadOnlyBarrierInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> Whitelist =
    [
        typeof(AuditLog),
        typeof(AccessLog),
        typeof(Notification),
    ];

    // Content a partner may author; create only — modify/delete of existing rows stays blocked.
    private static readonly HashSet<Type> PartnerAuthorable =
    [
        typeof(Document),
        typeof(Source),
        typeof(TaskforceMessage),
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
        var user = currentUserService.Get();
        Require(eventData.Context, user);
        return base.SavingChanges(eventData, result);
    }

    private static void Require(DbContext? context, CurrentUserInfo user)
    {
        if (context is null || (!user.IsOnlyReader && !user.IsPartner && !user.IsDemo))
        {
            return;
        }

        // partners may author content (create only); read-only supervision and demo stay fully read-only
        bool partnerMayAuthor = user.IsPartner && !user.IsOnlyReader && !user.IsDemo;

        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }
            var type = entry.Entity.GetType();
            if (Whitelist.Contains(type))
            {
                continue;
            }
            if (partnerMayAuthor && entry.State == EntityState.Added && PartnerAuthorable.Contains(type))
            {
                continue;
            }
            throw new UnauthorizedAccessException(
                "Nur-Lese-Modus: Änderungen sind in der Aufsichtsrolle nicht möglich.");
        }
    }
}
