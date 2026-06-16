using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Per-agency release state of a record or child item for a partner agency.</summary>
public record PartnerShareState(PartnerAgency Agency, bool Released, bool IncludesChildren);

/// <summary>Leadership-only partner releases: one active row per (entity, agency), whole-record or per child.</summary>
public interface IPartnerShareService
{
    /// <summary>Current release state per agency for a parent record (one entry per agency).</summary>
    Task<IReadOnlyList<PartnerShareState>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Current release state per agency for a single child item (one entry per agency).</summary>
    Task<IReadOnlyList<PartnerShareState>> GetForChildAsync(string childType, string childId, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the parent release for one agency; includesChildren = whole record.</summary>
    Task SetParentAsync(string entityType, string entityId, PartnerAgency agency, bool released, bool includesChildren, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the release of a single child item for one agency.</summary>
    Task SetChildAsync(string childType, string childId, PartnerAgency agency, bool released, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
