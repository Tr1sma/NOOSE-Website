using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;

namespace NOOSE_Website.Services;

/// <summary>Central visibility check for records.</summary>
public static class Visibility
{
    /// <summary>True if record is visible to the viewer; partners see only released, non-classified records.</summary>
    public static Task<bool> IsRecordVisibleAsync(
        AppDbContext db, string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        if (scope.PartnerAgency is { } agency)
        {
            return PartnerVisibility.IsRecordVisibleToPartnerAsync(db, entityType, entityId, agency, scope.MeId, cancellationToken);
        }
        return IsRecordVisibleAsync(db, entityType, entityId, scope.MayClassifiedRead, cancellationToken, scope.MeId);
    }

    /// <summary>True if record is visible to caller.</summary>
    public static async Task<bool> IsRecordVisibleAsync(
        AppDbContext db, string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null)
    {
        // leadership only
        if (entityType == nameof(Agent))
        {
            return isLeadership;
        }

        // taskforce check
        if (entityType == nameof(Taskforce))
        {
            return await TaskforceVisibility.IsVisibleAsync(db, entityId, isLeadership, meId, cancellationToken);
        }

        bool? classified = entityType switch
        {
            nameof(Person) => await db.People
                .Where(p => p.Id == entityId).Select(p => (bool?)p.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Faction) => await db.Factions
                .Where(f => f.Id == entityId).Select(f => (bool?)f.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // inherits parent
            nameof(FactionActivity) => await db.FactionActivities
                .Where(a => a.Id == entityId).Select(a => (bool?)a.Faction!.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(PersonGroup) => await db.PersonGroups
                .Where(g => g.Id == entityId).Select(g => (bool?)g.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Party) => await db.Parties
                .Where(p => p.Id == entityId).Select(p => (bool?)p.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Operation) => await db.Operations
                .Where(o => o.Id == entityId).Select(o => (bool?)o.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // handled above
            nameof(Case) => await db.Cases
                .Where(v => v.Id == entityId).Select(v => (bool?)v.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // always visible
            nameof(Job) => await db.Jobs
                .Where(a => a.Id == entityId).Select(a => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // always visible
            nameof(Appointment) => await db.Appointments
                .Where(t => t.Id == entityId).Select(t => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // any classification
            nameof(Document) => await db.Documents
                .Where(d => d.Id == entityId).Select(d => (bool?)(d.IsClassified || d.IsTRUClassified || d.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            // always visible
            nameof(Law) => await db.Laws
                .Where(g => g.Id == entityId).Select(g => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // no classification
            _ => false,
        };

        // unknown type
        if (entityType is not (nameof(Person) or nameof(Faction) or nameof(FactionActivity) or nameof(PersonGroup) or nameof(Party) or nameof(Operation) or nameof(Case) or nameof(Job) or nameof(Appointment) or nameof(Document) or nameof(Law)))
        {
            return true;
        }

        // null = not found
        return classified is not null && (isLeadership || classified == false);
    }
}
