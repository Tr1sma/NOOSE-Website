using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Generische Kommentare/Vermerke an beliebigen Akten (polymorph über EntitaetTyp + EntitaetId).
/// Sichtbarkeit richtet sich nach der Eltern-Akte; Löschen ist Soft-Delete und wird auditiert.
/// </summary>
public interface IKommentarService
{
    Task<List<Kommentar>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default);

    Task<Kommentar> ErstellenAsync(string entitaetTyp, string entitaetId, string text, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string kommentarId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
