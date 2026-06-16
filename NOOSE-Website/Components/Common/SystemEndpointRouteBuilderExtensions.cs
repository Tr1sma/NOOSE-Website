using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>Serves the admin-uploaded logo; anonymous access.</summary>
public static class SystemEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/system/logo", async (
            [FromServices] ISystemSettingService settings,
            CancellationToken cancellationToken) =>
        {
            var logo = await settings.LogoOpenAsync(cancellationToken);
            if (logo is null)
            {
                return Results.NotFound();
            }
            // no caching
            return Results.File(logo.Value.Content, logo.Value.ContentType);
        })
        .AllowAnonymous();
    }
}
