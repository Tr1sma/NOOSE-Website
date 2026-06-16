using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Resolves the current user from <see cref="HttpContext"/>, falling back to <see cref="AuthenticationStateProvider"/>.</summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
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

        // fallback to provider
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
                // system fallback
            }
        }

        return CurrentUserInfo.System;
    }

    private static CurrentUserInfo Build(ClaimsPrincipal user)
        => new(user.GetAgentId(), user.GetCodename() ?? user.Identity?.Name, user.IsOnlyReader());
}
