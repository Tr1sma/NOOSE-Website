using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Generisches Quellen-/Anhang-System: hängt Upload/Link/interne Verknüpfung/Freitext an eine beliebige
/// Akte (polymorph über EntitaetTyp + EntitaetId). Sichtbarkeit richtet sich nach der Eltern-Akte
/// (Verschlusssache/Papierkorb). Alle verändernden Aktionen werden auditiert; Löschen ist Soft-Delete.
/// </summary>
public interface ISourceService
{
    /// <summary>Sources of a record; visibility-filtered, partner-filtered (self-contained types + released items) when scope is a partner.</summary>
    Task<List<Source>> GetForRecordAsync(string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<Source> CreateAsync(string entityType, string entityId, SourceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RemoveAsync(string sourceId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Setzt das Anpinnen-Flag einer Quelle (oben in der Liste). Erfordert Schreibrecht.</summary>
    Task PinSetAsync(string sourceId, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Loads an upload source for the download endpoint and checks parent visibility (partner: child-release gated). Null if not accessible.</summary>
    Task<Source?> GetForDownloadAsync(string sourceId, ViewerScope scope, CancellationToken cancellationToken = default);
}
