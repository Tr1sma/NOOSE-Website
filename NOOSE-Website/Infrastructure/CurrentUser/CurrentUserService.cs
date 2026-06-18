using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Resolves the current user from <see cref="HttpContext"/>, falling back to <see cref="AuthenticationStateProvider"/>.</summary>
/// <remarks>Singleton; in a circuit there is no <c>HttpContext</c>, so the scoped <c>AuthenticationStateProvider</c> comes via <see cref="CircuitServicesAccessor"/>.</remarks>
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

        // no HttpContext in a circuit: get the circuit-scoped AuthenticationStateProvider via the accessor
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
                /* best effort */
            }
        }

        return CurrentUserInfo.System;
    }

    private static CurrentUserInfo Build(ClaimsPrincipal user)
        => new(user.GetAgentId(), user.GetCodename() ?? user.Identity?.Name, user.IsOnlyReader(), user.IsPartner());
}
