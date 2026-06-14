using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Generische Kommentare/Vermerke an beliebigen Akten (polymorph über EntitaetTyp + EntitaetId).
/// Sichtbarkeit richtet sich nach der Eltern-Akte; Löschen ist Soft-Delete und wird auditiert.
/// </summary>
public interface ICommentService
{
    Task<List<Comment>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default);

    Task<Comment> CreateAsync(string entityType, string entityId, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string commentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
