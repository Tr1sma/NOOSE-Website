using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
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
    IOptions<IdentityOptions> options,
    IConfiguration configuration)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    // demo instance: present every anonymous visitor as the demo agent unconditionally (no DB check)
    private readonly bool _forceDemo = configuration.GetValue<bool>("Demo:AutoSetup");

    // keep identical to the SecurityStampValidator interval in Program.cs
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // primary path: the middleware sets HttpContext.User, which the framework persists as the
        // circuit's state; this also backstops a reconnect that arrives anonymous. No SetAuthenticationState
        // here on purpose (would re-notify mid-resolution).
        AuthenticationState? state = null;
        try
        {
            state = await base.GetAuthenticationStateAsync();
        }
        catch
        {
            /* circuit without a seeded auth state: fall through to the demo backstop */
        }

        if (state?.User.Identity?.IsAuthenticated == true)
        {
            return state;
        }

        if (_forceDemo || await DemoActiveAsync())
        {
            return new AuthenticationState(DemoIdentity.BuildPrincipal());
        }

        return state ?? new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
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
