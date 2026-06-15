using System.Security.Claims;
using NOOSE_Website.Data.Entities.Watchlist;

namespace NOOSE_Website.Services;

/// <summary>
/// Watchlist: ein Agent „folgt" einer Akte (Person/Fraktion/…/Agent) und wird benachrichtigt, wenn diese sich
/// ändert (siehe <c>WatchlistAenderungInterceptor</c> + <c>WatchlistFanout</c>). Folgen/Entfolgen ist papierkorbfähig
/// (Entfolgen = Soft-Delete, erneutes Folgen reaktiviert die Zeile). Datenzugriff per <c>IDbContextFactory</c>.
/// </summary>
public interface IWatchlistService
{
    /// <summary>
    /// Der Aufrufer folgt der Akte (no-op, wenn bereits gefolgt). Gegated über <see cref="Sichtbarkeit"/> –
    /// einer nicht sichtbaren Akte (Verschlusssache ohne Führung, Papierkorb, oder Personalakte für Nicht-Führung)
    /// kann nicht gefolgt werden.
    /// </summary>
    Task FollowAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Der Aufrufer entfolgt der Akte (Soft-Delete der aktiven Zeile; no-op, wenn nicht gefolgt).</summary>
    Task UnfollowAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>True, wenn der Aufrufer der Akte aktuell folgt.</summary>
    Task<bool> IsFollowedAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Die aktiven Folge-Einträge des Aufrufers (für „Meine beobachteten Akten"), neueste zuerst.</summary>
    Task<List<WatchlistEntry>> GetFollowedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die gefolgten Akten des Aufrufers – bereits zu Anzeigename + Href aufgelöst und aus dessen Sicht
    /// Verschlusssache-/Papierkorb-geprüft (nicht mehr zugängliche bleiben in der Liste, aber ohne Name/Link, damit
    /// man sie entfolgen kann). Für die Seite „Meine beobachteten Akten".
    /// </summary>
    Task<List<FollowedRecord>> GetFollowedResolvedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Agent-Ids aller aktiven Folger einer Akte (für den Benachrichtigungs-Fan-out).</summary>
    Task<List<string>> GetFollowerIdsAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}

/// <summary>Eine gefolgte Akte, aufgelöst für die Anzeige in „Meine beobachteten Akten".</summary>
public sealed record FollowedRecord(string Type, string Id, string Display, string? Href, DateTime CreatedAt, bool Accessible);
