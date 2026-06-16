using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Read-visibility context: internal agent or external partner (released, non-classified only).</summary>
public readonly record struct ViewerScope(bool MayClassifiedRead, bool MayAllTaskforces, string? MeId, PartnerAgency? PartnerAgency)
{
    /// <summary>External partner viewer.</summary>
    public bool IsPartner => PartnerAgency is not null;

    /// <summary>Build from the current principal.</summary>
    public static ViewerScope From(ClaimsPrincipal user)
        => new(user.MayClassifiedRead(), user.MayAllTaskforcesSee(), user.GetAgentId(), user.GetPartnerAgency());
}
