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
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central visibility check for records.</summary>
public static class Visibility
{
    /// <summary>The three secrecy bool columns of a classifiable record.</summary>
    private sealed record SecrecyRow(bool Classified, bool Tru, bool Hrb);

    /// <summary>Level for records where IsClassified means "restricted at all" (Person/Faction/…/Case).</summary>
    private static DocumentClassification LevelRestricted(SecrecyRow r)
        => !r.Classified ? DocumentClassification.None
            : r.Tru ? DocumentClassification.Tru
            : r.Hrb ? DocumentClassification.Hrb
            : DocumentClassification.Leadership;

    /// <summary>Level for documents where IsClassified is leadership-exclusive.</summary>
    private static DocumentClassification LevelDocument(SecrecyRow r)
        => r.Classified ? DocumentClassification.Leadership
            : r.Tru ? DocumentClassification.Tru
            : r.Hrb ? DocumentClassification.Hrb
            : DocumentClassification.None;

    /// <summary>True if record is visible to the viewer; partners see only released, non-classified records.</summary>
    public static async Task<bool> IsRecordVisibleAsync(
        AppDbContext db, string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        if (scope.PartnerAgency is { } agency)
        {
            return await PartnerVisibility.IsRecordVisibleToPartnerAsync(db, entityType, entityId, agency, scope.MeId, cancellationToken);
        }
        // taskforces gate on membership (MayAllTaskforces), not the classified-read flag
        if (entityType == nameof(Taskforce))
        {
            return await TaskforceVisibility.IsVisibleAsync(db, entityId, scope.MayAllTaskforces, scope.MeId, cancellationToken);
        }
        // leadership only
        if (entityType == nameof(Agent))
        {
            return scope.MayClassifiedRead;
        }
        // applications: HRB or leadership, mirrors the page policy
        if (entityType == nameof(Bewerbung))
        {
            return (scope.MayClassifiedRead || scope.IsHrb)
                && await db.Bewerbungen.AnyAsync(b => b.Id == entityId, cancellationToken);
        }

        SecrecyRow? row = entityType switch
        {
            nameof(Person) => await db.People
                .Where(p => p.Id == entityId)
                .Select(p => new SecrecyRow(p.IsClassified, p.IsTRUClassified, p.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Faction) => await db.Factions
                .Where(f => f.Id == entityId)
                .Select(f => new SecrecyRow(f.IsClassified, f.IsTRUClassified, f.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            // inherits parent faction
            nameof(FactionActivity) => await db.FactionActivities
                .Where(a => a.Id == entityId)
                .Select(a => new SecrecyRow(a.Faction!.IsClassified, a.Faction!.IsTRUClassified, a.Faction!.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            nameof(PersonGroup) => await db.PersonGroups
                .Where(g => g.Id == entityId)
                .Select(g => new SecrecyRow(g.IsClassified, g.IsTRUClassified, g.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Party) => await db.Parties
                .Where(p => p.Id == entityId)
                .Select(p => new SecrecyRow(p.IsClassified, p.IsTRUClassified, p.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Operation) => await db.Operations
                .Where(o => o.Id == entityId)
                .Select(o => new SecrecyRow(o.IsClassified, o.IsTRUClassified, o.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Case) => await db.Cases
                .Where(v => v.Id == entityId)
                .Select(v => new SecrecyRow(v.IsClassified, v.IsTRUClassified, v.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Document) => await db.Documents
                .Where(d => d.Id == entityId)
                .Select(d => new SecrecyRow(d.IsClassified, d.IsTRUClassified, d.IsHRBClassified))
                .FirstOrDefaultAsync(cancellationToken),
            _ => null,
        };

        // classifiable types: visible per the viewer's secrecy scope (null = not found)
        if (entityType is nameof(Person) or nameof(Faction) or nameof(FactionActivity) or nameof(PersonGroup)
            or nameof(Party) or nameof(Operation) or nameof(Case))
        {
            return row is not null && scope.CanSee(LevelRestricted(row));
        }
        if (entityType == nameof(Document))
        {
            return row is not null && scope.CanSee(LevelDocument(row));
        }

        // always visible, but the record must exist
        return entityType switch
        {
            nameof(Job) => await db.Jobs.AnyAsync(a => a.Id == entityId, cancellationToken),
            nameof(Appointment) => await db.Appointments.AnyAsync(t => t.Id == entityId, cancellationToken),
            nameof(Law) => await db.Laws.AnyAsync(g => g.Id == entityId, cancellationToken),
            // unknown type = visible
            _ => true,
        };
    }

    /// <summary>True if record is visible to caller; leadership-scoped shim around the full scope check.</summary>
    public static Task<bool> IsRecordVisibleAsync(
        AppDbContext db, string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null)
        => IsRecordVisibleAsync(db, entityType, entityId, new ViewerScope(isLeadership, isLeadership, meId, null), cancellationToken);
}
