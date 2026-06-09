using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Personen-Doks (Verhöre/Maßnahmen). Beim Anlegen wirkt der Maßnahme-Ausgang auf
/// den Lebensstatus der Person (Erschossen → temporärer Tod; Amnestie-Spritze → Gedächtnisverlust).
/// </summary>
public interface IPersonDokService
{
    Task<List<PersonDok>> GetFuerPersonAsync(string personId, CancellationToken cancellationToken = default);
    Task<PersonDok> ErstellenAsync(string personId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string dokId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
