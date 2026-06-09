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

    /// <summary>Alle Doks (übergreifend) inkl. zugehöriger Person; respektiert den Verschlusssachen-Filter.</summary>
    Task<List<PersonDok>> GetAlleAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    Task<PersonDok> ErstellenAsync(string personId, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt ein Dok für eine <b>neue</b> Person an: erstellt zunächst die Akte (nur mit Namen) und
    /// hängt das Dok daran. Genutzt vom übergreifenden „Neues Dok"-Dialog, wenn die Person noch nicht existiert.
    /// </summary>
    Task<PersonDok> ErstellenFuerNeuePersonAsync(string name, PersonDokEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string dokId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
