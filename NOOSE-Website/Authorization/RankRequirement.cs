using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>Requires at least the given rank, or admin.</summary>
public class RankRequirement : IAuthorizationRequirement
{
    public RankRequirement(Rank minimum) => Minimum = minimum;

    public Rank Minimum { get; }
}
