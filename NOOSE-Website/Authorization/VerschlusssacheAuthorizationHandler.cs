using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Stub-Handler für <see cref="VerschlusssacheRequirement"/>: lässt in Phase 1 Führung/Admin
/// durch. Ab der Akten-Phase wird hier zusätzlich die konkrete Akte als Ressource geprüft
/// (markiert als Verschlusssache? Agent ausdrücklich zugewiesen?).
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
