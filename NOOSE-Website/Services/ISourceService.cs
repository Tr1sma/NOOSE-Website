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
    /// <summary><paramref name="meId"/> = Agent-Id des Aufrufers; nur für Taskforce-Akten nötig
    /// (Mitgliedschafts-Sichtbarkeit) – für andere Typen ohne Belang.</summary>
    Task<List<Source>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null);

    Task<Source> CreateAsync(string entityType, string entityId, SourceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RemoveAsync(string sourceId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Setzt das Anpinnen-Flag einer Quelle (oben in der Liste). Erfordert Schreibrecht.</summary>
    Task PinSetAsync(string sourceId, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt eine Upload-Quelle für den Download-Endpoint und prüft die Sichtbarkeit der Eltern-Akte.
    /// Liefert null, wenn die Quelle nicht existiert, kein Upload ist oder die Akte nicht zugänglich ist.
    /// <paramref name="meId"/> = Agent-Id des Aufrufers (nur für Taskforce-Akten relevant).
    /// </summary>
    Task<Source?> GetForDownloadAsync(string sourceId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null);
}
