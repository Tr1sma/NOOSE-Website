using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Account;

/// <summary>Carries auth state into interactive components and revalidates it (kill-switch). In demo mode it presents anonymous circuits as the demo agent and never revalidates that synthetic principal away.</summary>
internal sealed class DemoAwareAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    // keep identical to the SecurityStampValidator interval in Program.cs
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // primary path: the middleware sets HttpContext.User, which the framework persists as the
        // circuit's state; this only backstops a reconnect that arrives anonymous. No SetAuthenticationState
        // here on purpose (would re-notify mid-resolution).
        var state = await base.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated == true)
        {
            return state;
        }

        return await DemoActiveAsync()
            ? new AuthenticationState(DemoIdentity.BuildPrincipal())
            : state;
    }

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // the synthetic demo principal is static and always valid
        if (authenticationState.User.IsDemo())
        {
            return true;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Agent>>();
        return await ValidateAsync(userManager, authenticationState.User);
    }

    private async Task<bool> DemoActiveAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingService>();
        return (await settings.GetAsync()).DemoModeActive;
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
