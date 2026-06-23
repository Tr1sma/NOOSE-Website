using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure;

/// <summary>While demo mode is on, presents anonymous visitors as the read-only demo agent so the whole app is browsable without login. Login and framework paths stay anonymous.</summary>
public sealed class DemoModeMiddleware(RequestDelegate next)
{
    // login + framework + asset paths must not be hijacked
    private static readonly string[] ExcludedPrefixes =
    [
        "/Account", "/signin-discord", "/health", "/_blazor", "/_framework", "/system/logo",
    ];

    public async Task InvokeAsync(HttpContext context, ISystemSettingService settings)
    {
        if (context.User.Identity?.IsAuthenticated != true && !IsExcluded(context.Request.Path))
        {
            var config = await settings.GetAsync(context.RequestAborted);
            if (config.DemoModeActive)
            {
                context.User = DemoIdentity.BuildPrincipal();
            }
        }

        await next(context);
    }

    private static bool IsExcluded(PathString path)
    {
        foreach (var prefix in ExcludedPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
