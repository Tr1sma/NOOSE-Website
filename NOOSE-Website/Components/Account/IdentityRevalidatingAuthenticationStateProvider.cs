using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Components.Account;

/// <summary>
/// Trägt den Authentifizierungs-Status in die interaktiven Server-Komponenten und revalidiert ihn
/// regelmäßig. Die Revalidierung ist zugleich der <b>Kill-Switch</b>: Stimmt der SecurityStamp nicht
/// mehr (Sperre/Rangänderung erneuern ihn) oder ist der Agent nicht mehr <see cref="AgentStatus.Aktiv"/>,
/// wird der Circuit getrennt und der Nutzer abgemeldet.
/// </summary>
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    // Kurzes Intervall, damit eine Notfall-Sperre praktisch sofort greift.
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

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
        if (agent is null || agent.Status != AgentStatus.Aktiv)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var stampImCookie = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        var aktuellerStamp = await userManager.GetSecurityStampAsync(agent);
        return stampImCookie == aktuellerStamp;
    }
}
