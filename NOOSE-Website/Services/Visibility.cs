using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Financing;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central visibility check for records.</summary>
public static class Visibility
{
    /// <summary>The three secrecy bool columns of a classifiable record.</summary>
    private sealed record SecrecyRow(bool Classified, bool Tru, bool Hrb);

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
        // (bool-shim callers like mention-notification fanout can only carry the leadership flag, so keep MayClassifiedRead here)
        if (entityType == nameof(Bewerbung))
        {
            return (scope.MayClassifiedRead || scope.IsHrb)
                && await db.Bewerbungen.AnyAsync(b => b.Id == entityId, cancellationToken);
        }
        // funding requests: the requester plus anyone who reads everything (leadership/supervision).
        // MUST stay explicit — the tail of this method treats an unknown type as visible to all.
        if (entityType == nameof(FinancingRequest))
        {
            return await FinancingVisibility.IsVisibleAsync(db, entityId, scope.MayClassifiedRead, scope.MeId, cancellationToken);
        }
        // documents: secrecy level alone is not the gate — the owning taskforce and per-agent revocation count too
        if (entityType == nameof(Document))
        {
            return await DocumentVisibility.IsVisibleAsync(db, entityId, scope.AsDocumentScope(), cancellationToken);
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
            _ => null,
        };

        // classifiable types: visible per the viewer's secrecy scope (null = not found)
        if (entityType is nameof(Person) or nameof(Faction) or nameof(PersonGroup)
            or nameof(Party) or nameof(Operation) or nameof(Case))
        {
            return row is not null && RecordVisibility.IsVisible(scope, row.Classified, row.Tru, row.Hrb);
        }

        // always visible, but the record must exist
        return entityType switch
        {
            nameof(Job) => await db.Jobs.AnyAsync(a => a.Id == entityId, cancellationToken),
            nameof(Appointment) => await db.Appointments.AnyAsync(t => t.Id == entityId, cancellationToken),
            nameof(Law) => await db.Laws.AnyAsync(g => g.Id == entityId, cancellationToken),
            nameof(Meeting) => await db.Meetings.AnyAsync(m => m.Id == entityId, cancellationToken),
            // agenda: rank/supervision at once, any other internal agent 2h after the meeting, partners never
            nameof(MeetingAgendaItem) => await AgendaItemVisibleAsync(db, entityId, scope, cancellationToken),
            // unknown type = visible
            _ => true,
        };
    }

    /// <summary>Agenda item visibility: leadership keeps its immediate access; other internal agents gain it 2h after the meeting.</summary>
    private static async Task<bool> AgendaItemVisibleAsync(
        AppDbContext db, string itemId, ViewerScope scope, CancellationToken cancellationToken)
    {
        // preserve the exact rank-gate behaviour: existence only, independent of the meeting time
        if (scope.MayAgenda)
        {
            return await db.MeetingAgendaItems.AnyAsync(p => p.Id == itemId, cancellationToken);
        }
        var when = await db.MeetingAgendaItems.AsNoTracking()
            .Where(p => p.Id == itemId)
            .Join(db.Meetings, p => p.MeetingId, m => m.Id, (p, m) => new { m.Start, m.End })
            .FirstOrDefaultAsync(cancellationToken);
        return when is not null && MeetingVisibility.MayReadAgenda(scope, when.Start, when.End, DateTime.UtcNow);
    }

    /// <summary>True if record is visible to caller; leadership-scoped shim around the full scope check.</summary>
    public static Task<bool> IsRecordVisibleAsync(
        AppDbContext db, string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null)
        // leadership (rank 4) implies the agenda rank (3), so the shim may carry it
        => IsRecordVisibleAsync(db, entityType, entityId,
               new ViewerScope(isLeadership, isLeadership, meId, null, MayAgenda: isLeadership), cancellationToken);
}
