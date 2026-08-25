using System.Security.Claims;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Reads and writes the on/off state of the public area.</summary>
public interface IPublicModuleService
{
    /// <summary>Cached snapshot of every catalog module plus the kill switch.</summary>
    Task<PublicModuleSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Effective state including the kill switch; an unknown key is never enabled.</summary>
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Throws when the module is off, so a write path cannot rely on the UI alone.</summary>
    Task RequireEnabledAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Text to show on the module's route while it is off.</summary>
    Task<string> OfflineTextAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Nav tabs of enabled modules, in display order.</summary>
    Task<IReadOnlyList<PublicModuleState>> NavEntriesAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IEnumerable<PublicModuleInput> rows, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task KillSwitchSetAsync(bool active, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
