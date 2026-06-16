using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>Requires a partner of the given agency at or above the given rank tier.</summary>
public class PartnerRankRequirement : IAuthorizationRequirement
{
    public PartnerRankRequirement(PartnerAgency agency, PartnerRank minimum)
    {
        Agency = agency;
        Minimum = minimum;
    }

    public PartnerAgency Agency { get; }
    public PartnerRank Minimum { get; }
}
