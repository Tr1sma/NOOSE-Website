using System.Security.Claims;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Factions;

namespace NOOSE_Website.Services;

/// <summary>Faction records: list/detail, CRUD, classification, members, agents, history.</summary>
public interface IFactionService
{
    Task<List<Faction>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<Faction?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<Faction>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Faction>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<Faction> CreateAsync(FactionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, FactionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set classification; "Gesichert staatsgefährdend" requires Senior Special Agent+ or Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Append-only classification history of the faction, newest first; visibility-filtered.</summary>
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<List<FactionMember>> GetMembersAsync(string factionId, ViewerScope scope, CancellationToken cancellationToken = default);
    Task MemberAddAsync(string factionId, MemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberChangeAsync(string memberId, string? rank, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>NOOSE agents allocated to the faction (investigation leads first).</summary>
    Task<List<FactionAgent>> GetAgentsAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Allocations marked as investigation lead.</summary>
    Task<List<FactionAgent>> GetInvestigationLeadAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Allocate an agent; lead flag is leadership-only.</summary>
    Task AgentAllocateAsync(string factionId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the investigation-lead flag; leadership only.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit entries for the faction and its memberships; visibility-filtered.</summary>
    Task<List<AuditLog>> GetHistoryAsync(string factionId, bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Faction's own activity log, newest first; visibility-filtered, partner-filtered when scope is a partner.</summary>
    Task<List<FactionActivity>> GetActivitiesAsync(string factionId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task ActivityAddAsync(string factionId, ActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task ActivityChangeAsync(string activityId, ActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task ActivityRemoveAsync(string activityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Activity kinds already in use, for the free-text-with-suggestions picker.</summary>
    Task<List<string>> GetActivityKindsAsync(CancellationToken cancellationToken = default);

    /// <summary>Faction photos (title image first, then by capture time).</summary>
    Task<List<FactionPhoto>> GetPhotosAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Loads a photo with its faction for the protected endpoint, gated to the viewer (partner: child-release gated); null if not visible.</summary>
    Task<FactionPhoto?> GetPhotoWithFactionAsync(string photoId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Add a photo; the first photo becomes the title image.</summary>
    Task<FactionPhoto> PhotoAddAsync(string factionId, Stream content, string originalName, string contentType, long size, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task PhotoRemoveAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Mark the given photo as title image; clears the flag on all others.</summary>
    Task AsTitleImageSetAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
