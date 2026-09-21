using System.Security.Claims;
using NOOSE_Website.Data.Entities.Radio;
using NOOSE_Website.Models.Radio;

namespace NOOSE_Website.Services;

/// <summary>Radio plan: who talks on which channel, and the reverse lookup for an overheard frequency.</summary>
public interface IRadioService
{
    /// <summary>The whole plan, already filtered for this viewer.</summary>
    Task<RadioPlan> GetPlanAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One channel, or null when it does not exist or the viewer may not see it.</summary>
    Task<RadioChannel?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<RadioChannel> CreateAsync(RadioChannelInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, RadioChannelInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<RadioChannel>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
