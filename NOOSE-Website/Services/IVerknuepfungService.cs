using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Generische Verknüpfungs-Engine: legt gerichtete Verknüpfungen zwischen beliebigen Akten an und liefert
/// sie aus Sicht einer Akte <b>bidirektional</b> normalisiert („andere Seite"). Ziele sind Personen,
/// Fraktionen oder Personengruppen; Verschlusssache-/Papierkorb-Sichtbarkeit wird über die jeweilige Akte
/// geprüft. Über <see cref="VerknuepfungArt"/> getrennt: allgemeine Verknüpfungen vs. Konflikte/Bündnisse.
/// </summary>
public interface IVerknuepfungService
{
    /// <summary>Verknüpfungen einer Akte; mit <paramref name="art"/> auf eine Beziehungsart eingeschränkt (null = alle).</summary>
    Task<List<VerknuepfungAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, VerknuepfungArt? art = null, CancellationToken cancellationToken = default);

    Task ErstellenAsync(string vonTyp, string vonId, string nachTyp, string nachId, string? label, ClaimsPrincipal handelnder, VerknuepfungArt art = VerknuepfungArt.Standard, CancellationToken cancellationToken = default);

    Task EntfernenAsync(string verknuepfungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
