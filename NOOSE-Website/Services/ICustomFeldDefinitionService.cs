using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>Verwaltung der admin-definierten Custom-Feld-Definitionen (Zusatzfelder je Aktentyp).</summary>
public interface ICustomFeldDefinitionService
{
    /// <summary>Alle Definitionen (Admin-Liste, inkl. inaktive), sortiert nach Aktentyp/Reihenfolge/Name.</summary>
    Task<List<CustomFeldDefinition>> GetAlleAsync(CancellationToken cancellationToken = default);

    /// <summary>Definitionen eines Aktentyps; <paramref name="nurAktive"/> für das Panel.</summary>
    Task<List<CustomFeldDefinition>> GetFuerTypAsync(string entitaetTyp, bool nurAktive, CancellationToken cancellationToken = default);

    Task<CustomFeldDefinition> ErstellenAsync(CustomFeldDefinitionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task AktualisierenAsync(string id, CustomFeldDefinitionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
