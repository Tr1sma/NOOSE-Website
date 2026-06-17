using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Components.Account;

/// <summary>Carries auth state into interactive server components and revalidates it; doubles as the kill-switch (stale SecurityStamp or non-active agent disconnects and signs out).</summary>
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    // keep identical to the SecurityStampValidator interval in Program.cs (~30s worst-case lockout latency)
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Agent>>();
        return await ValidateAsync(userManager, authenticationState.User);
    }

    private async Task<bool> ValidateAsync(UserManager<Agent> userManager, ClaimsPrincipal principal)
    {
        var agent = await userManager.GetUserAsync(principal);
        if (agent is null || agent.Status != AgentStatus.Active)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var stampInCookie = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        var currentStamp = await userManager.GetSecurityStampAsync(agent);
        return stampInCookie == currentStamp;
    }
}
