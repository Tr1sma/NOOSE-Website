using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Tag/label master data and polymorphic assignment to any record.</summary>
public interface ITagService
{
    Task<List<Tag>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>All tags with their assignment counts.</summary>
    Task<List<TagUsage>> GetWithUsageAsync(CancellationToken cancellationToken = default);

    Task<Tag> CreateAsync(string name, string? colour, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string tagId, string name, string? colour, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a tag; assignments are removed via FK cascade.</summary>
    Task DeleteAsync(string tagId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<Tag>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a record's tag assignments with the given set (diff update).</summary>
    Task SetAsync(string entityType, string entityId, IEnumerable<string> tagIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
