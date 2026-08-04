using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IValueListLabelService" />
public class ValueListLabelService(IDbContextFactory<AppDbContext> dbFactory) : IValueListLabelService
{
    public async Task SetAsync(string list, string key, string label, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        label = (label ?? string.Empty).Trim();
        if (label.Length is 0 or > 200)
        {
            throw new InvalidOperationException("Die Bezeichnung darf nicht leer und höchstens 200 Zeichen lang sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.EnumLabelOverrides
            .FirstOrDefaultAsync(o => o.List == list && o.Key == key, cancellationToken);
        if (row is null)
        {
            db.EnumLabelOverrides.Add(new EnumLabelOverride { List = list, Key = key, Label = label });
        }
        else
        {
            row.Label = label;
        }
        // EnumLabelOverride is not IAuditable → record the label change explicitly
        db.AuditLogs.Add(ManualAudit.Row(nameof(EnumLabelOverride), $"{list}:{key}", AuditAction.Modified, actor,
            ManualAudit.Change("Bezeichnung", null, label)));
        await db.SaveChangesAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }

    public async Task ResetAsync(string list, string key, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        // ExecuteDelete bypasses the SaveChanges read-only barrier, so write access is enforced here
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.EnumLabelOverrides
            .Where(o => o.List == list && o.Key == key)
            .ExecuteDeleteAsync(cancellationToken);
        // audit manually: ExecuteDelete bypassed the interceptor
        db.AuditLogs.Add(ManualAudit.Row(nameof(EnumLabelOverride), $"{list}:{key}", AuditAction.Deleted, actor));
        await db.SaveChangesAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }

    // refresh the static store so all running circuits pick up the new labels
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.EnumLabelOverrides
            .Select(o => new { o.List, o.Key, o.Label })
            .ToListAsync(cancellationToken);
        EnumLabelText.ReplaceAll(rows.Select(o => (o.List, o.Key, o.Label)));
    }
}
