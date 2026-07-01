using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Read-visibility context: internal agent or external partner (released, non-classified only).</summary>
public readonly record struct ViewerScope(
    bool MayClassifiedRead, bool MayAllTaskforces, string? MeId, PartnerAgency? PartnerAgency,
    bool IsTru = false, bool IsHrb = false, bool IsLeadership = false)
{
    /// <summary>External partner viewer.</summary>
    public bool IsPartner => PartnerAgency is not null;

    /// <summary>May see recruiting (applications): HRB or leadership, never read-only supervision (which must not see real names).</summary>
    public bool MayRecruiting => IsLeadership || IsHrb;

    /// <summary>Build from the current principal.</summary>
    public static ViewerScope From(ClaimsPrincipal user)
        => new(user.MayClassifiedRead(), user.MayAllTaskforcesSee(), user.GetAgentId(), user.GetPartnerAgency(),
               user.IsTRU(), user.IsHRB(), user.IsLeadership());

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
