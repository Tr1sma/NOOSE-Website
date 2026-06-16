using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Per-agency release state of a record or child item for a partner agency.</summary>
public record PartnerShareState(PartnerAgency Agency, bool Released, bool IncludesChildren);

/// <summary>Release of a record to a single partner account.</summary>
public record PartnerIndividualShareState(string AgentId, string Codename, PartnerAgency Agency, PartnerRank? Rank, bool IncludesChildren);

/// <summary>Selectable partner account for an individual release.</summary>
public record PartnerAccountOption(string AgentId, string Codename, PartnerAgency Agency, PartnerRank? Rank);

/// <summary>Release coverage of one record type for an agency: total records and how many are released agency-wide.</summary>
public record PartnerTypeShareSummary(string TypeKey, string DisplayName, int TotalRecords, int SharedRecords);

/// <summary>Leadership-only partner releases: one active row per (entity, agency, account), whole-record or per child.</summary>
public interface IPartnerShareService
{
    /// <summary>Current release state per agency for a parent record (one entry per agency; excludes individual releases).</summary>
    Task<IReadOnlyList<PartnerShareState>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Current release state per agency for a single child item (one entry per agency; excludes individual releases).</summary>
    Task<IReadOnlyList<PartnerShareState>> GetForChildAsync(string childType, string childId, CancellationToken cancellationToken = default);

    /// <summary>Individual account releases for a parent record.</summary>
    Task<IReadOnlyList<PartnerIndividualShareState>> GetIndividualSharesForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Active partner accounts that can receive an individual release.</summary>
    Task<IReadOnlyList<PartnerAccountOption>> GetSelectablePartnersAsync(CancellationToken cancellationToken = default);

    /// <summary>Set/clear the parent release for one agency; includesChildren = whole record.</summary>
    Task SetParentAsync(string entityType, string entityId, PartnerAgency agency, bool released, bool includesChildren, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the release of a single child item for one agency.</summary>
    Task SetChildAsync(string childType, string childId, PartnerAgency agency, bool released, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the parent release for a single partner account; agency is taken from the account.</summary>
    Task SetIndividualParentAsync(string entityType, string entityId, string partnerAgentId, bool released, bool includesChildren, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Release coverage per releasable type for one agency (agency-wide shares only).</summary>
    Task<IReadOnlyList<PartnerTypeShareSummary>> GetTypeSummariesAsync(PartnerAgency agency, CancellationToken cancellationToken = default);

    /// <summary>Release or withdraw a whole record type for one agency at once; returns how many records were newly released (or withdrawn).</summary>
    Task<int> SetTypeAsync(string entityType, PartnerAgency agency, bool released, bool includesChildren, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
