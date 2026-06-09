using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Personen-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/
/// Bearbeiten mit Steckbrief-Kindern, Papierkorb (Soft-Delete/Wiederherstellen), Einstufung mit
/// Rang-Gate, Foto-Galerie und Akten-Historie. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IPersonService
{
    Task<List<Person>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Person?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Person>> GetPapierkorbAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sucht Personen nach Name oder Aktenzeichen (für Auswahl-/Autocomplete-Felder). Liefert die
    /// ersten Treffer alphabetisch; respektiert den Verschlusssachen-Filter.
    /// </summary>
    Task<List<Person>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Mögliche Dubletten anhand identischem Namen oder gemeinsamer Telefonnummer.</summary>
    Task<List<Person>> FindeDuplikateAsync(string name, IEnumerable<string> telefonnummern, CancellationToken cancellationToken = default);

    Task<Person> ErstellenAsync(PersonEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, PersonEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Append-only Einstufungs-Verlauf der Person (neueste zuerst).</summary>
    Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Fraktionen/Personengruppen, denen die Person angehört (Rück-Verknüpfungen, Verschlusssache-gefiltert).</summary>
    Task<List<PersonZugehoerigkeit>> GetZugehoerigkeitenAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default);

    Task<PersonFoto> FotoHinzufuegenAsync(string personId, Stream inhalt, string originalName, string contentType, long groesse, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task FotoEntfernenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Lädt ein Foto inkl. zugehöriger Person (für Auslieferung/Sichtbarkeitsprüfung).</summary>
    Task<PersonFoto?> GetFotoMitPersonAsync(string fotoId, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Person und ihrer Doks (für die Akten-Historie).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string personId, CancellationToken cancellationToken = default);
}
