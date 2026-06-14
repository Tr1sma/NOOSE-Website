using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Observationen (Überwachungseinträge an Personen) – getrennt von den Verhör-Doks.
/// Reine Protokoll-Akte ohne Lebensstatus-Logik und ohne eigenes Aktenzeichen; erbt die
/// Verschlusssache-Sichtbarkeit von der zugehörigen Person.
/// </summary>
public interface IObservationService
{
    /// <summary>Observationen einer Person inkl. aufgelöstem Agent-Deckname und (Verschlusssache-gefilterter) Org-Anzeige.</summary>
    Task<List<ObservationDisplay>> GetForPersonAsync(string personId, bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Alle Observationen (übergreifend) inkl. zugehöriger Person; respektiert den Verschlusssachen-Filter.</summary>
    Task<List<ObservationDisplay>> GetAllAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>
    /// Observationen, die mit einer Organisation (Fraktion/Personengruppe) verknüpft sind (Rück-Verknüpfung).
    /// <paramref name="orgTyp"/> ist <c>nameof(Fraktion)</c> bzw. <c>nameof(Personengruppe)</c>; Verschlusssache-gefiltert.
    /// </summary>
    Task<List<ObservationDisplay>> GetForOrgAsync(string orgType, string orgId, bool isLeadership, CancellationToken cancellationToken = default);

    Task<Observation> CreateAsync(string personId, ObservationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<Observation> RefreshAsync(string observationId, ObservationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string observationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
