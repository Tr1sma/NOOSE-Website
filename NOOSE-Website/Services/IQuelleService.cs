using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Generisches Quellen-/Anhang-System: hängt Upload/Link/interne Verknüpfung/Freitext an eine beliebige
/// Akte (polymorph über EntitaetTyp + EntitaetId). Sichtbarkeit richtet sich nach der Eltern-Akte
/// (Verschlusssache/Papierkorb). Alle verändernden Aktionen werden auditiert; Löschen ist Soft-Delete.
/// </summary>
public interface IQuelleService
{
    /// <summary><paramref name="meId"/> = Agent-Id des Aufrufers; nur für Taskforce-Akten nötig
    /// (Mitgliedschafts-Sichtbarkeit) – für andere Typen ohne Belang.</summary>
    Task<List<Quelle>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default, string? meId = null);

    Task<Quelle> ErstellenAsync(string entitaetTyp, string entitaetId, QuelleEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task EntfernenAsync(string quelleId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Setzt das Anpinnen-Flag einer Quelle (oben in der Liste). Erfordert Schreibrecht.</summary>
    Task AnpinnenSetzenAsync(string quelleId, bool angepinnt, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt eine Upload-Quelle für den Download-Endpoint und prüft die Sichtbarkeit der Eltern-Akte.
    /// Liefert null, wenn die Quelle nicht existiert, kein Upload ist oder die Akte nicht zugänglich ist.
    /// <paramref name="meId"/> = Agent-Id des Aufrufers (nur für Taskforce-Akten relevant).
    /// </summary>
    Task<Quelle?> GetFuerDownloadAsync(string quelleId, bool istFuehrung, CancellationToken cancellationToken = default, string? meId = null);
}
