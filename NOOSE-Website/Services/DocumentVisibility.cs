using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Read gate of a library document. Three independent layers, all three required.</summary>
/// <remarks>
/// Existed only inside <see cref="DocumentService"/> before, while <see cref="Visibility.IsRecordVisibleAsync"/>
/// applied the first layer alone — so a taskforce-internal document, or one an agent had been explicitly excluded
/// from, stayed reachable through its comments, sources, followups, links, custom fields, requests and the NOOSEI
/// record anchor. This is the one place where the point-check was weaker than the list path.
/// </remarks>
public static class DocumentVisibility
{
    /// <summary>Secrecy level of a document. Unlike a record, here <c>IsClassified</c> means leadership-exclusive
    /// rather than "restricted at all" — same three columns, different reading.</summary>
    public static DocumentClassification LevelOf(bool classified, bool tru, bool hrb)
        => classified ? DocumentClassification.Leadership
            : tru ? DocumentClassification.Tru
            : hrb ? DocumentClassification.Hrb
            : DocumentClassification.None;

    /// <summary>Documents the viewer may see: secrecy level, owning taskforce, and per-agent revocation.</summary>
    public static IQueryable<Document> OnlyVisible(this IQueryable<Document> query, AppDbContext db, DocumentViewerScope scope)
    {
        // locals so EF parameterizes rather than baking the viewer's flags into the SQL
        bool mayClassified = scope.MayClassified, isTru = scope.IsTru, isHrb = scope.IsHrb;
        bool isLeadership = scope.IsLeadership, isAdmin = scope.IsAdmin;
        string? meId = scope.MeId;
        return query
            .Where(d => (!d.IsClassified && !d.IsTRUClassified && !d.IsHRBClassified)
                || mayClassified
                || (d.IsTRUClassified && isTru)
                || (d.IsHRBClassified && isHrb))
            // taskforce-internal: members and leadership/admin only
            .Where(d => d.OwnerTaskforceId == null
                || isLeadership
                || (meId != null && db.TaskforceAgents.Any(ta => ta.TaskforceId == d.OwnerTaskforceId && ta.AgentId == meId)))
            // per-agent revocation (admins always retain access)
            .Where(d => isAdmin || meId == null
                || !db.DocumentAccessExclusions.Any(x => x.DocumentId == d.Id && x.AgentId == meId));
    }

    /// <summary>Point-check. Missing and invisible are the same answer — the caller must not learn which.</summary>
    public static Task<bool> IsVisibleAsync(
        AppDbContext db, string documentId, DocumentViewerScope scope, CancellationToken cancellationToken = default)
        => db.Documents.OnlyVisible(db, scope).AnyAsync(d => d.Id == documentId, cancellationToken);

    /// <summary>Of a candidate id set, those the viewer may see. Absent = hidden.</summary>
    public static async Task<HashSet<string>> VisibleIdsAsync(
        AppDbContext db, IReadOnlyCollection<string> documentIds, DocumentViewerScope scope,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        var ids = await db.Documents.OnlyVisible(db, scope)
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }
}
