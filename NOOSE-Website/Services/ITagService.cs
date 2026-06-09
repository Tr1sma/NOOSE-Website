using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung von Tags/Labels (Stammdaten, Führung) und deren Zuordnung zu beliebigen Akten
/// (polymorph über EntitaetTyp + EntitaetId; Taggen darf jeder aktive Agent).
/// </summary>
public interface ITagService
{
    Task<List<Tag>> GetAlleAsync(CancellationToken cancellationToken = default);

    /// <summary>Alle Tags samt Anzahl ihrer Zuordnungen (für die Tag-Verwaltung).</summary>
    Task<List<TagVerwendung>> GetMitVerwendungAsync(CancellationToken cancellationToken = default);

    Task<Tag> ErstellenAsync(string name, string? farbe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string tagId, string name, string? farbe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Löscht ein Tag hart; die Zuordnungen werden per FK-Cascade mitentfernt.</summary>
    Task LoeschenAsync(string tagId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task<List<Tag>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, CancellationToken cancellationToken = default);

    /// <summary>Ersetzt die Tag-Zuordnungen einer Akte durch die übergebene Menge (Differenz-Update).</summary>
    Task SetzenAsync(string entitaetTyp, string entitaetId, IEnumerable<string> tagIds, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
