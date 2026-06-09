using System.Security.Claims;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Generische Verknüpfungs-Engine: legt gerichtete Verknüpfungen zwischen beliebigen Akten an und liefert
/// sie aus Sicht einer Akte <b>bidirektional</b> normalisiert („andere Seite"). In Phase 3 sind Ziele
/// stets Personen; Verschlusssache-/Papierkorb-Sichtbarkeit wird über die jeweilige Akte geprüft.
/// </summary>
public interface IVerknuepfungService
{
    Task<List<VerknuepfungAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default);

    Task ErstellenAsync(string vonTyp, string vonId, string nachTyp, string nachId, string? label, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task EntfernenAsync(string verknuepfungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
