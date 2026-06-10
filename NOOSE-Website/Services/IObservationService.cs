using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Observationen (Überwachungseinträge an Personen) – getrennt von den Verhör-Doks.
/// Reine Protokoll-Akte ohne Lebensstatus-Logik und ohne eigenes Aktenzeichen; erbt die
/// Verschlusssache-Sichtbarkeit von der zugehörigen Person.
/// </summary>
public interface IObservationService
{
    /// <summary>Observationen einer Person inkl. aufgelöstem Agent-Deckname und (Verschlusssache-gefilterter) Org-Anzeige.</summary>
    Task<List<ObservationAnzeige>> GetFuerPersonAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Alle Observationen (übergreifend) inkl. zugehöriger Person; respektiert den Verschlusssachen-Filter.</summary>
    Task<List<ObservationAnzeige>> GetAlleAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>
    /// Observationen, die mit einer Organisation (Fraktion/Personengruppe) verknüpft sind (Rück-Verknüpfung).
    /// <paramref name="orgTyp"/> ist <c>nameof(Fraktion)</c> bzw. <c>nameof(Personengruppe)</c>; Verschlusssache-gefiltert.
    /// </summary>
    Task<List<ObservationAnzeige>> GetFuerOrgAsync(string orgTyp, string orgId, bool istFuehrung, CancellationToken cancellationToken = default);

    Task<Observation> ErstellenAsync(string personId, ObservationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task<Observation> AktualisierenAsync(string observationId, ObservationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string observationId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
