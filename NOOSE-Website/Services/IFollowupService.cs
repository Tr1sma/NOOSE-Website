using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Terminierte Wiedervorlagen/Erinnerungen an Akten. Mehrere je Akte möglich (polymorph über EntitaetTyp/EntitaetId).
/// Bei Fälligkeit benachrichtigt der <c>WiedervorlageFaelligkeitsDienst</c> den Zuständigen und die Follower der Akte.
/// Datenzugriff per <c>IDbContextFactory</c>; alle Akten-bezogenen Methoden sind Verschlusssache-/Papierkorb-geprüft.
/// </summary>
public interface IFollowupService
{
    /// <summary>Alle Wiedervorlagen einer Akte (offene zuerst). Leer, wenn die Akte für den Aufrufer nicht sichtbar ist.</summary>
    Task<List<FollowupItem>> GetForRecordAsync(string entityType, string entityId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Legt eine Wiedervorlage an (Akte muss sichtbar sein). Default-Zuständiger = Ersteller.</summary>
    Task CreateAsync(string entityType, string entityId, FollowupInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Ändert Termin/Notiz/Zuständigen (Ersteller, Zuständiger oder Führung).</summary>
    Task RefreshAsync(string id, FollowupInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Hakt eine Wiedervorlage ab (Ersteller, Zuständiger oder Führung).</summary>
    Task CompleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Öffnet eine erledigte Wiedervorlage wieder (Ersteller, Zuständiger oder Führung).</summary>
    Task ReopenAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Löscht eine Wiedervorlage (Soft-Delete; Ersteller oder Führung).</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offene, fällige Wiedervorlagen des Aufrufers (zuständig ODER folgt der Akte), aufgelöst zu Anzeigename + Href
    /// und aus dessen Sicht Verschlusssache-/Papierkorb-geprüft. Für das Dashboard.
    /// </summary>
    Task<List<FollowupDashboardItem>> GetMyDueAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
