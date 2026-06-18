using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central file library; classification level (Leadership/TRU/HRB) only settable by, and visible to, the matching unit.</summary>
public interface ILibraryService
{
    Task<List<LibraryFile>> GetListAsync(DocumentViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Upload a file (type/size validated in the storage service).</summary>
    Task<LibraryFile> UploadAsync(string title, string? category, DocumentClassification classification,
        Stream content, string originalName, string contentType, long sizeBytes,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Change title/category/classification (level only by those entitled).</summary>
    Task RefreshAsync(string id, string title, string? category, DocumentClassification classification,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Download endpoint: returns the file only when visible to the caller.</summary>
    Task<LibraryFile?> GetForDownloadAsync(string id, DocumentViewerScope scope, CancellationToken cancellationToken = default);
}
