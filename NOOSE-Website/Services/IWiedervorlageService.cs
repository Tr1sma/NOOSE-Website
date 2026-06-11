using System.Security.Claims;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Terminierte Wiedervorlagen/Erinnerungen an Akten. Mehrere je Akte möglich (polymorph über EntitaetTyp/EntitaetId).
/// Bei Fälligkeit benachrichtigt der <c>WiedervorlageFaelligkeitsDienst</c> den Zuständigen und die Follower der Akte.
/// Datenzugriff per <c>IDbContextFactory</c>; alle Akten-bezogenen Methoden sind Verschlusssache-/Papierkorb-geprüft.
/// </summary>
public interface IWiedervorlageService
{
    /// <summary>Alle Wiedervorlagen einer Akte (offene zuerst). Leer, wenn die Akte für den Aufrufer nicht sichtbar ist.</summary>
    Task<List<WiedervorlageItem>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default);

    /// <summary>Legt eine Wiedervorlage an (Akte muss sichtbar sein). Default-Zuständiger = Ersteller.</summary>
    Task ErstellenAsync(string entitaetTyp, string entitaetId, WiedervorlageEingabe eingabe, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default);

    /// <summary>Ändert Termin/Notiz/Zuständigen (Ersteller, Zuständiger oder Führung).</summary>
    Task AktualisierenAsync(string id, WiedervorlageEingabe eingabe, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default);

    /// <summary>Hakt eine Wiedervorlage ab (Ersteller, Zuständiger oder Führung).</summary>
    Task ErledigenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Öffnet eine erledigte Wiedervorlage wieder (Ersteller, Zuständiger oder Führung).</summary>
    Task WiedereroeffnenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Löscht eine Wiedervorlage (Soft-Delete; Ersteller oder Führung).</summary>
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offene, fällige Wiedervorlagen des Aufrufers (zuständig ODER folgt der Akte), aufgelöst zu Anzeigename + Href
    /// und aus dessen Sicht Verschlusssache-/Papierkorb-geprüft. Für das Dashboard.
    /// </summary>
    Task<List<WiedervorlageDashboardItem>> GetMeineFaelligenAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
