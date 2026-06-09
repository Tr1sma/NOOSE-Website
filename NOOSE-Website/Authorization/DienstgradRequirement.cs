using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Verlangt mindestens den angegebenen <see cref="Dienstgrad"/> – oder Admin.
/// Geprueft vom <see cref="DienstgradAuthorizationHandler"/>.
/// </summary>
public class DienstgradRequirement : IAuthorizationRequirement
{
    public DienstgradRequirement(Dienstgrad minimum) => Minimum = minimum;

    public Dienstgrad Minimum { get; }
}
