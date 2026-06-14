using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Personen-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/
/// Bearbeiten mit Steckbrief-Kindern, Papierkorb (Soft-Delete/Wiederherstellen), Einstufung mit
/// Rang-Gate, Foto-Galerie und Akten-Historie. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IPersonService
{
    Task<List<Person>> GetListAsync(bool isLeadership, CancellationToken cancellationToken = default);
    Task<Person?> GetDetailAsync(string id, bool isLeadership, CancellationToken cancellationToken = default);
    Task<List<Person>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sucht Personen nach Name oder Aktenzeichen (für Auswahl-/Autocomplete-Felder). Liefert die
    /// ersten Treffer alphabetisch; respektiert den Verschlusssachen-Filter.
    /// </summary>
    Task<List<Person>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Mögliche Dubletten anhand identischem Namen oder gemeinsamer Telefonnummer (Verschlusssache-gefiltert).</summary>
    Task<List<Person>> FindDuplicatesAsync(string name, IEnumerable<string> phoneNumbers, bool isLeadership, CancellationToken cancellationToken = default);

    Task<Person> CreateAsync(PersonInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, PersonInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Append-only Einstufungs-Verlauf der Person (neueste zuerst; Verschlusssache-gefiltert).</summary>
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Fraktionen/Personengruppen, denen die Person aktuell angehört (Rück-Verknüpfungen, Verschlusssache-gefiltert).</summary>
    Task<List<PersonAffiliation>> GetAffiliationsAsync(string personId, bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ehemalige (beendete) Zugehörigkeiten der Person mit Beitritts-/Austrittsdatum, neueste zuerst –
    /// für den Mitgliedschafts-Verlauf. Verschlusssache-gefiltert; Akten im Papierkorb werden ausgeblendet.
    /// </summary>
    Task<List<PersonAffiliation>> GetFormerAffiliationsAsync(string personId, bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abgeleitete Verbündete/Gegner: Mitglieder von Organisationen, die mit einer Organisation der Person
    /// verbündet/verfeindet sind (berechnet, nicht gespeichert; Verschlusssache-gefiltert).
    /// </summary>
    Task<List<DerivedRelation>> GetDerivedRelationsAsync(string personId, bool isLeadership, CancellationToken cancellationToken = default);

    Task<PersonPhoto> PhotoAddAsync(string personId, Stream content, string originalName, string contentType, long size, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task PhotoRemoveAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Lädt ein Foto inkl. zugehöriger Person (für Auslieferung/Sichtbarkeitsprüfung).</summary>
    Task<PersonPhoto?> GetPhotoWithPersonAsync(string photoId, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Person und ihrer Doks (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string personId, bool isLeadership, CancellationToken cancellationToken = default);
}
