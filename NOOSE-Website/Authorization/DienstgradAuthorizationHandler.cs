using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Erfuellt eine <see cref="DienstgradRequirement"/>, wenn der Agent Admin ist oder sein
/// Dienstgrad mindestens dem geforderten Minimum entspricht.
/// </summary>
public class DienstgradAuthorizationHandler : AuthorizationHandler<DienstgradRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DienstgradRequirement requirement)
    {
        var user = context.User;

        if (user.IstAdmin())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var dienstgrad = user.GetDienstgrad();
        if (dienstgrad.HasValue && (int)dienstgrad.Value >= (int)requirement.Minimum)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
