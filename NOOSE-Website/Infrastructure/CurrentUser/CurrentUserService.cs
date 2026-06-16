using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Resolves the current user from <see cref="HttpContext"/>, falling back to <see cref="AuthenticationStateProvider"/>.</summary>
/// <remarks>
/// Registriert als Singleton (damit es in die jetzt singleton-registrierten SaveChanges-Interceptors passt).
/// Im Blazor-Circuit gibt es keinen <c>HttpContext</c>; den dort scoped <c>AuthenticationStateProvider</c>
/// liefert der <see cref="CircuitServicesAccessor"/> (AsyncLocal des aktuellen Circuit-Scopes).
/// </remarks>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor, CircuitServicesAccessor circuitServices)
    : ICurrentUserService
{
    public CurrentUserInfo Get()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        return httpUser?.Identity?.IsAuthenticated == true ? Build(httpUser) : CurrentUserInfo.System;
    }

    public async Task<CurrentUserInfo> GetAsync()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            return Build(httpUser);
        }

        // Kein HttpContext im Circuit → den circuit-scoped AuthenticationStateProvider über den Accessor holen.
        // (Außerhalb von Circuit + HTTP, z. B. Hintergrund-Worker, bleibt es bei System.)
        var authProvider = circuitServices.Services?.GetService(typeof(AuthenticationStateProvider)) as AuthenticationStateProvider;
        if (authProvider is not null)
        {
            try
            {
                var state = await authProvider.GetAuthenticationStateAsync();
                if (state.User.Identity?.IsAuthenticated == true)
                {
                    return Build(state.User);
                }
            }
            catch
            {
                // system fallback
            }
        }

        return CurrentUserInfo.System;
    }

    private static CurrentUserInfo Build(ClaimsPrincipal user)
        => new(user.GetAgentId(), user.GetCodename() ?? user.Identity?.Name, user.IsOnlyReader(), user.IsPartner());
}
