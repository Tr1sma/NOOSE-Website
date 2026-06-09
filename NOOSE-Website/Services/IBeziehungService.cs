using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Typisierte Person-zu-Person-Beziehungen (Familie/Feind/…). Liefert sie aus Sicht einer Person
/// (egal auf welcher Seite gespeichert); blendet Gegenseiten im Papierkorb sowie – für Nicht-Führung –
/// Verschlusssachen aus.
/// </summary>
public interface IBeziehungService
{
    Task<List<BeziehungAnzeige>> GetFuerPersonAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default);

    Task ErstellenAsync(string personAId, string personBId, BeziehungsTyp typ, string? notiz, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task EntfernenAsync(string beziehungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
