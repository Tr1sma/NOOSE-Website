using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Erfüllt eine <see cref="DienstgradRequirement"/>, wenn der Agent Admin ist oder sein
/// Dienstgrad mindestens dem geforderten Minimum entspricht.
/// </summary>
public class RankAuthorizationHandler : AuthorizationHandler<RankRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RankRequirement requirement)
    {
        var user = context.User;

        if (user.IsAdmin())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var rank = user.GetRank();
        if (rank.HasValue && (int)rank.Value >= (int)requirement.Minimum)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
