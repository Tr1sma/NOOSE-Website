using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Stub-Handler fuer <see cref="VerschlusssacheRequirement"/>: laesst in Phase 1 Fuehrung/Admin
/// durch. Ab der Akten-Phase wird hier zusaetzlich die konkrete Akte als Ressource geprueft
/// (markiert als Verschlusssache? Agent ausdruecklich zugewiesen?).
/// </summary>
public class VerschlusssacheAuthorizationHandler : AuthorizationHandler<VerschlusssacheRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VerschlusssacheRequirement requirement)
    {
        if (context.User.IstFuehrung())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
