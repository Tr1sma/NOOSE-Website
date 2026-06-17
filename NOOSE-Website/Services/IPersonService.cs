using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>Business logic for person records: list/detail, CRUD with profile children, classification, photos, history.</summary>
public interface IPersonService
{
    Task<List<Person>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<Person?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<Person>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>Search people by name or case number for internal pickers (write path); classified-filtered.</summary>
    Task<List<Person>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Possible duplicates by identical name or shared phone number (classified-filtered).</summary>
    Task<List<Person>> FindDuplicatesAsync(string name, IEnumerable<string> phoneNumbers, bool isLeadership, CancellationToken cancellationToken = default);

    Task<Person> CreateAsync(PersonInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, PersonInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set classification. "Secured state-threatening" requires Senior Special Agent+ or Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Append-only classification history of the person, newest first; visibility-filtered.</summary>
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Factions/person-groups the person currently belongs to (back-links); visibility-filtered.</summary>
    Task<List<PersonAffiliation>> GetAffiliationsAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Former affiliations with join/leave dates, newest first; visibility-filtered, trashed parents hidden.</summary>
    Task<List<PersonAffiliation>> GetFormerAffiliationsAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Derived allies/enemies from allied/hostile orgs of the person's orgs (computed; visibility-filtered).</summary>
    Task<List<DerivedRelation>> GetDerivedRelationsAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<PersonPhoto> PhotoAddAsync(string personId, Stream content, string originalName, string contentType, long size, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task PhotoRemoveAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Loads a photo with its person for delivery, gated to the viewer (partner: child-release gated); null if not visible.</summary>
    Task<PersonPhoto?> GetPhotoWithPersonAsync(string photoId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Audit entries of the person and its docs (record history; visibility-filtered).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);
}
