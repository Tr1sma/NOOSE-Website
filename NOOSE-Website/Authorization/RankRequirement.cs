using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Verlangt mindestens den angegebenen <see cref="Dienstgrad"/> – oder Admin.
/// Geprüft vom <see cref="DienstgradAuthorizationHandler"/>.
/// </summary>
public class RankRequirement : IAuthorizationRequirement
{
    public RankRequirement(Rank minimum) => Minimum = minimum;

    public Rank Minimum { get; }
}
