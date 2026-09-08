using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Images pasted into plain-text fields: stored on a record, referenced from the text by a mention token.</summary>
public interface ITextImageService
{
    /// <summary>Stores a pasted image on a record and returns the mention token to put into the text.</summary>
    Task<string?> SaveAsync(string entityType, string entityId, byte[] content, string contentType,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>What the delivery endpoint needs, or null when the viewer may not see the carrying record.</summary>
    Task<TextImageAccess?> GetAccessAsync(string id, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
