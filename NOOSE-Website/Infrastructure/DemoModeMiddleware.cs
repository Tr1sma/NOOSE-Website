using Microsoft.Extensions.Configuration;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Infrastructure;

/// <summary>While demo mode is on, presents anonymous visitors as the read-only demo agent so the whole app is browsable without login. Login and framework paths stay anonymous.</summary>
public sealed class DemoModeMiddleware(RequestDelegate next, IConfiguration configuration)
{
    // demo instance: present every anonymous visitor as the demo agent unconditionally (no DB check)
    private readonly bool _forceDemo = configuration.GetValue<bool>("Demo:AutoSetup");

    // login + framework + asset paths must not be hijacked
    private static readonly string[] InfrastructurePrefixes =
    [
        "/Account", "/signin-discord", "/health", "/_blazor", "/_framework", "/system/logo",
    ];

    /// <summary>Infrastructure plus every public route: an anonymous visitor outside must stay anonymous.</summary>
    /// <remarks>
    /// Derived from <see cref="PublicRoutes"/> rather than repeated. /gesucht used to be listed by hand, and
    /// /gefasst is a sibling route rather than a child of it — it would have been missed the same way.
    /// </remarks>
    public static readonly string[] ExcludedPrefixes = [.. InfrastructurePrefixes, .. PublicRoutes.Prefixes];

    public async Task InvokeAsync(HttpContext context, ISystemSettingService settings)
    {
        if (context.User.Identity?.IsAuthenticated != true && !IsExcluded(context.Request.Path))
        {
            if (_forceDemo || (await settings.GetAsync(context.RequestAborted)).DemoModeActive)
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
