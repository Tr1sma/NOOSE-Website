using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>Observation entries on persons; inherits classification visibility from the owning person.</summary>
public interface IObservationService
{
    /// <summary>Observations of a person with resolved agent codename and (visibility-filtered) org display; partner-filtered when scope is a partner.</summary>
    Task<List<ObservationDisplay>> GetForPersonAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>All observations incl. owning person; visibility-filtered.</summary>
    Task<List<ObservationDisplay>> GetAllAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Observations linked to an organization (faction/person group); visibility-filtered.</summary>
    Task<List<ObservationDisplay>> GetForOrgAsync(string orgType, string orgId, bool isLeadership, CancellationToken cancellationToken = default);

    Task<Observation> CreateAsync(string personId, ObservationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<Observation> RefreshAsync(string observationId, ObservationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string observationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
