using System.Security.Claims;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Services;

/// <summary>Informant visibility. Every internal agent sees every V-person file in full; partners never.
/// Record access is all-or-nothing — there is no second tier, and the assigned handler is no longer a gate.</summary>
public static class InformantVisibility
{
    /// <summary>May see informant records at all — and with them every field on them.</summary>
    public static bool MaySeeRecord(ClaimsPrincipal actor) => !actor.IsPartner();

    /// <summary>Same rule, from a scope rather than a principal.</summary>
    public static bool MaySeeRecord(ViewerScope scope) => !scope.IsPartner;
}
