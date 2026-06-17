using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>System settings: maintenance mode, banner, theme colors, logo upload. Reads are cached; writes are admin-only and invalidate the cache.</summary>
public interface ISystemSettingService
{
    /// <summary>Current configuration (cached, ~10 s). Falls back to defaults on DB errors.</summary>
    Task<SystemConfiguration> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves maintenance/banner/theme colors. Admin only; colors must be empty or #RRGGBB.</summary>
    Task SaveAsync(SystemConfigurationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Uploads a new logo (images only, photo-upload size limit). Admin only.</summary>
    Task LogoSetAsync(Stream content, string originalName, string contentType, long sizeBytes, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Removes the uploaded logo (back to the default crest). Admin only.</summary>
    Task LogoRemoveAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Opens the current logo file for delivery; null if none is set.</summary>
    Task<(Stream Content, string ContentType)?> LogoOpenAsync(CancellationToken cancellationToken = default);
}
