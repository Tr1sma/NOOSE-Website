using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central document library (WYSIWYG HTML documents); visibility by classification level, leadership always sees all levels.</summary>
public interface IDocumentService
{
    /// <summary>Visible documents, newest first; classified-filtered, or released-only when partnerAgency is set.</summary>
    Task<List<DocumentListItem>> GetListAsync(DocumentViewerScope scope, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);

    /// <summary>Typeahead search over title/category for the attach picker.</summary>
    Task<List<DocumentListItem>> SearchAsync(string? searchText, DocumentViewerScope scope, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Single document with HTML body, or null if missing/not visible; released-only when partnerAgency is set.</summary>
    Task<Document?> GetAsync(string id, DocumentViewerScope scope, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);

    Task<Document> CreateAsync(DocumentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, DocumentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the pinned mark; leadership only.</summary>
    Task PinSetAsync(string id, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (trash); creator or leadership only.</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Records this document is attached to as a source; foreign taskforces are hidden by meId.</summary>
    Task<List<DocumentAttachment>> GetAttachmentsAsync(string documentId, bool isLeadership, string? meId, CancellationToken cancellationToken = default);
}
