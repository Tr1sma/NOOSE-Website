using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Admin writes for the DB-backed display-name overrides of code-defined value lists.</summary>
public interface IValueListLabelService
{
    /// <summary>Creates or updates the display-name override for one enum member and refreshes the static override store.</summary>
    Task SetAsync(string list, string key, string label, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Removes the override so the code default shows again and refreshes the static override store.</summary>
    Task ResetAsync(string list, string key, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
