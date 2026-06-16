using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Personen-Doks (Verhöre/Maßnahmen). Beim Anlegen wirkt der Maßnahme-Ausgang auf
/// den Lebensstatus der Person (Erschossen → temporärer Tod; Amnestie-Spritze → Gedächtnisverlust).
/// </summary>
public interface IPersonDocService
{
    /// <summary>Docs of a person with resolved (visibility-filtered) link display; partner-filtered when scope is a partner.</summary>
    Task<List<PersonDocDisplay>> GetForPersonAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Alle Doks (übergreifend) inkl. zugehöriger Person und aufgelöster Verknüpfung; respektiert den Verschlusssachen-Filter.</summary>
    Task<List<PersonDocDisplay>> GetAllAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Docs linked to an org (faction/group); visibility-filtered, partner-filtered when scope is a partner.</summary>
    Task<List<PersonDocDisplay>> GetForOrgAsync(string orgType, string orgId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<PersonDoc> CreateAsync(string personId, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt ein Dok für eine <b>neue</b> Person an: erstellt zunächst die Akte (nur mit Namen) und
    /// hängt das Dok daran. Genutzt vom übergreifenden „Neues Dok"-Dialog, wenn die Person noch nicht existiert.
    /// </summary>
    Task<PersonDoc> CreateForNewPersonAsync(string name, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bearbeitet ein bestehendes Dok. Der Maßnahme-Ausgang wird neu ausgewertet und wirkt – sofern das
    /// aktuelle Tot-Fenster der Person von genau diesem Dok stammt – erneut auf deren Lebensstatus.
    /// </summary>
    Task<PersonDoc> RefreshAsync(string docId, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string docId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
