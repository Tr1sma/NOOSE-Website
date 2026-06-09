using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>
/// Liest den aktuellen Agent zuerst aus dem <see cref="HttpContext"/> (deckt OAuth-Endpoints und
/// die initiale SSR-Anfrage ab) und faellt sonst auf den Blazor-<see cref="AuthenticationStateProvider"/>
/// zurueck (deckt interaktive Circuit-Updates ab, in denen kein HttpContext mehr existiert).
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    : ICurrentUserService
{
    public async Task<CurrentUserInfo> GetAsync()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            return Build(httpUser);
        }

        // Kein HttpContext (interaktiver Circuit) → den scoped AuthenticationStateProvider befragen.
        var authProvider = serviceProvider.GetService(typeof(AuthenticationStateProvider)) as AuthenticationStateProvider;
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
                // Ausserhalb eines Circuits nicht verfuegbar – als System behandeln.
            }
        }

        return CurrentUserInfo.System;
    }

    private static CurrentUserInfo Build(ClaimsPrincipal user)
        => new(user.GetAgentId(), user.GetAnzeigename() ?? user.Identity?.Name);
}
