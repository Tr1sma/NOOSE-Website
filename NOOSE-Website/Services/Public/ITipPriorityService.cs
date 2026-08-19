using NOOSE_Website.Data;

namespace NOOSE_Website.Services.Public;

/// <summary>Keeps the cached <c>Prioritaet</c> of open tips in step with bounty, hazard level and trust tier.</summary>
/// <remarks>
/// The only writer of that column, and it depends on nothing but the context factory — that is what lets
/// <c>PublicWantedService</c> and <c>BountyService</c> call it without a DI cycle, since <c>TipService</c> already
/// depends on <c>IPublicWantedService</c>.
/// </remarks>
public interface ITipPriorityService
{
    /// <summary>Priority for a tip that is about to be inserted; runs on the caller's context and transaction.</summary>
    Task<int> ComputeAsync(AppDbContext db, string? wantedId, int confirmedTips,
        CancellationToken cancellationToken = default);

    /// <summary>Re-stamps one tip.</summary>
    Task StampAsync(string tipId, CancellationToken cancellationToken = default);

    /// <summary>Re-stamps every open tip on one notice; bounty or hazard level changed.</summary>
    Task StampForNoticeAsync(string wantedId, CancellationToken cancellationToken = default);

    /// <summary>Re-stamps every open tip of one citizen; the trust tier changed.</summary>
    Task StampForCitizenAsync(string citizenProfileId, CancellationToken cancellationToken = default);
}
