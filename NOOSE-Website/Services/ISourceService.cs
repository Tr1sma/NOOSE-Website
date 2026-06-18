using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Generic source/attachment system: attaches upload/link/internal-link/text to any record polymorphically; visibility follows the parent record.</summary>
public interface ISourceService
{
    /// <summary>Sources of a record; visibility-filtered, partner-filtered (self-contained types + released items) when scope is a partner.</summary>
    Task<List<Source>> GetForRecordAsync(string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<Source> CreateAsync(string entityType, string entityId, SourceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RemoveAsync(string sourceId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Sets a source's pinned flag (top of the list). Requires write access.</summary>
    Task PinSetAsync(string sourceId, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Loads an upload source for the download endpoint and checks parent visibility (partner: child-release gated). Null if not accessible.</summary>
    Task<Source?> GetForDownloadAsync(string sourceId, ViewerScope scope, CancellationToken cancellationToken = default);
}
