using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>Succeeds when the viewer is a partner of the required agency at or above the required rank.</summary>
public class PartnerRankAuthorizationHandler : AuthorizationHandler<PartnerRankRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PartnerRankRequirement requirement)
    {
        if (context.User.HasPartnerRank(requirement.Agency, requirement.Minimum))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
