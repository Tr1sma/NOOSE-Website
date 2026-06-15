using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung von Tags/Labels (Stammdaten, Führung) und deren Zuordnung zu beliebigen Akten
/// (polymorph über EntitaetTyp + EntitaetId; Taggen darf jeder aktive Agent).
/// </summary>
public interface ITagService
{
    Task<List<Tag>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Alle Tags samt Anzahl ihrer Zuordnungen (für die Tag-Verwaltung).</summary>
    Task<List<TagUsage>> GetWithUsageAsync(CancellationToken cancellationToken = default);

    Task<Tag> CreateAsync(string name, string? colour, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string tagId, string name, string? colour, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Löscht ein Tag hart; die Zuordnungen werden per FK-Cascade mitentfernt.</summary>
    Task DeleteAsync(string tagId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<Tag>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Ersetzt die Tag-Zuordnungen einer Akte durch die übergebene Menge (Differenz-Update).</summary>
    Task SetAsync(string entityType, string entityId, IEnumerable<string> tagIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
