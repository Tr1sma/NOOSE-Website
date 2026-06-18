using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Partner visibility: a record is visible only if released to the viewer's agency or account, and not classified.</summary>
public static class PartnerVisibility
{
    /// <summary>Record types that can be released to partners; everything else is never partner-visible.</summary>
    public static bool IsReleasableType(string entityType) => entityType is
        nameof(Person) or nameof(Faction) or nameof(PersonGroup) or nameof(Party)
        or nameof(Operation) or nameof(Case) or nameof(Document) or nameof(Law)
        or nameof(Taskforce);

    /// <summary>True if an active share row grants this record to the agency or the viewer's account (whole or shell).</summary>
    public static Task<bool> HasShareAsync(AppDbContext db, string entityType, string entityId, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
        => db.PartnerShares.AnyAsync(s => s.EntityType == entityType && s.EntityId == entityId && s.Agency == agency
            && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId), cancellationToken);

    /// <summary>Parent point-check: released to agency or account, exists, and not classified.</summary>
    public static async Task<bool> IsRecordVisibleToPartnerAsync(
        AppDbContext db, string entityType, string entityId, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
    {
        if (!IsReleasableType(entityType))
        {
            return false;
        }
        if (!await HasShareAsync(db, entityType, entityId, agency, partnerAgentId, cancellationToken))
        {
            return false;
        }

        // null=missing, true=classified, false=ok
        bool? classified = entityType switch
        {
            nameof(Person) => await db.People
                .Where(p => p.Id == entityId).Select(p => (bool?)p.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(Faction) => await db.Factions
                .Where(f => f.Id == entityId).Select(f => (bool?)f.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(PersonGroup) => await db.PersonGroups
                .Where(g => g.Id == entityId).Select(g => (bool?)g.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(Party) => await db.Parties
                .Where(p => p.Id == entityId).Select(p => (bool?)p.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(Operation) => await db.Operations
                .Where(o => o.Id == entityId).Select(o => (bool?)o.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(Taskforce) => await db.Taskforces
                .Where(t => t.Id == entityId).Select(t => (bool?)t.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(Case) => await db.Cases
                .Where(v => v.Id == entityId).Select(v => (bool?)v.IsClassified).FirstOrDefaultAsync(cancellationToken),
            nameof(Document) => await db.Documents
                .Where(d => d.Id == entityId).Select(d => (bool?)(d.IsClassified || d.IsTRUClassified || d.IsHRBClassified || d.OwnerTaskforceId != null)).FirstOrDefaultAsync(cancellationToken),
            nameof(Law) => await db.Laws
                .Where(g => g.Id == entityId).Select(g => (bool?)false).FirstOrDefaultAsync(cancellationToken),
            _ => null,
        };

        return classified == false;
    }

    /// <summary>Child point-check: parent partner-visible AND (parent whole-record OR an explicit child release).</summary>
    public static async Task<bool> IsChildVisibleToPartnerAsync(
        AppDbContext db, string parentType, string parentId, string childType, string childId, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
    {
        if (!await IsRecordVisibleToPartnerAsync(db, parentType, parentId, agency, partnerAgentId, cancellationToken))
        {
            return false;
        }
        var whole = await db.PartnerShares.AnyAsync(
            s => s.EntityType == parentType && s.EntityId == parentId && s.Agency == agency && s.IncludesChildren
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId), cancellationToken);
        return whole || await HasShareAsync(db, childType, childId, agency, partnerAgentId, cancellationToken);
    }

    /// <summary>True if the parent record is released whole (all children covered) to the agency or the viewer's account.</summary>
    public static Task<bool> ParentIncludesChildrenAsync(AppDbContext db, string parentType, string parentId, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
        => db.PartnerShares.AnyAsync(s => s.EntityType == parentType && s.EntityId == parentId && s.Agency == agency && s.IncludesChildren
            && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId), cancellationToken);

    /// <summary>Of a candidate child-id set, those individually released to the agency or the viewer's account.</summary>
    public static async Task<HashSet<string>> ReleasedChildIdsAsync(AppDbContext db, string childType, IReadOnlyCollection<string> childIds, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
    {
        if (childIds.Count == 0)
        {
            return new();
        }
        var ids = await db.PartnerShares
            .Where(s => s.EntityType == childType && s.Agency == agency && childIds.Contains(s.EntityId)
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId))
            .Select(s => s.EntityId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    /// <summary>Filters a child list for a partner: all when the parent is released whole, else only individually released items.</summary>
    public static async Task<List<T>> FilterChildrenAsync<T>(AppDbContext db, string parentType, string parentId, string childType,
        List<T> items, Func<T, string> idSelector, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0 || await ParentIncludesChildrenAsync(db, parentType, parentId, agency, partnerAgentId, cancellationToken))
        {
            return items;
        }
        var released = await ReleasedChildIdsAsync(db, childType, items.Select(idSelector).ToList(), agency, partnerAgentId, cancellationToken);
        return items.Where(i => released.Contains(idSelector(i))).ToList();
    }

    /// <summary>Of a candidate parent-id set, those released to the agency or the viewer's account. Caller still applies the classified check.</summary>
    public static async Task<HashSet<string>> ReleasedParentIdsAsync(
        AppDbContext db, string entityType, IReadOnlyCollection<string> candidateIds, PartnerAgency agency, string? partnerAgentId, CancellationToken cancellationToken = default)
    {
        if (candidateIds.Count == 0)
        {
            return new();
        }
        var ids = await db.PartnerShares
            .Where(s => s.EntityType == entityType && s.Agency == agency && candidateIds.Contains(s.EntityId)
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId))
            .Select(s => s.EntityId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    // ---- list predicates: released (agency or account) and not classified ----

    public static IQueryable<Person> OnlyPartnerVisible(this IQueryable<Person> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(p => !p.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(Person) && s.EntityId == p.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Faction> OnlyPartnerVisible(this IQueryable<Faction> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(f => !f.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(Faction) && s.EntityId == f.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<PersonGroup> OnlyPartnerVisible(this IQueryable<PersonGroup> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(g => !g.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(PersonGroup) && s.EntityId == g.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Party> OnlyPartnerVisible(this IQueryable<Party> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(p => !p.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(Party) && s.EntityId == p.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Operation> OnlyPartnerVisible(this IQueryable<Operation> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(o => !o.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(Operation) && s.EntityId == o.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Case> OnlyPartnerVisible(this IQueryable<Case> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(v => !v.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(Case) && s.EntityId == v.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Document> OnlyPartnerVisible(this IQueryable<Document> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(d => !(d.IsClassified || d.IsTRUClassified || d.IsHRBClassified) && d.OwnerTaskforceId == null
            && db.PartnerShares.Any(s => s.EntityType == nameof(Document) && s.EntityId == d.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Law> OnlyPartnerVisible(this IQueryable<Law> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(l => db.PartnerShares.Any(s => s.EntityType == nameof(Law) && s.EntityId == l.Id && s.Agency == agency
            && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));

    public static IQueryable<Taskforce> OnlyPartnerVisible(this IQueryable<Taskforce> query, AppDbContext db, PartnerAgency agency, string? partnerAgentId)
        => query.Where(t => !t.IsClassified
            && db.PartnerShares.Any(s => s.EntityType == nameof(Taskforce) && s.EntityId == t.Id && s.Agency == agency
                && (s.PartnerAgentId == null || s.PartnerAgentId == partnerAgentId)));
}
