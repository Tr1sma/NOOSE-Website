using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Read-visibility context: internal agent or external partner (released, non-classified only).</summary>
public readonly record struct ViewerScope(
    bool MayClassifiedRead, bool MayAllTaskforces, string? MeId, PartnerAgency? PartnerAgency,
    bool IsTru = false, bool IsHrb = false)
{
    /// <summary>External partner viewer.</summary>
    public bool IsPartner => PartnerAgency is not null;

    /// <summary>Build from the current principal.</summary>
    public static ViewerScope From(ClaimsPrincipal user)
        => new(user.MayClassifiedRead(), user.MayAllTaskforcesSee(), user.GetAgentId(), user.GetPartnerAgency(),
               user.IsTRU(), user.IsHRB());

    /// <summary>True if this viewer may see a record at the given secrecy level.</summary>
    public bool CanSee(DocumentClassification level) => level switch
    {
        DocumentClassification.None => true,
        DocumentClassification.Leadership => MayClassifiedRead,
        DocumentClassification.Tru => MayClassifiedRead || IsTru,
        DocumentClassification.Hrb => MayClassifiedRead || IsHrb,
        _ => false,
    };
}
