using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>Delivery of images pasted into text fields; the carrying record decides, not the token.</summary>
/// <remarks>
/// Internal agents only and never under a public prefix: the picture sits on a comment, a chat line or a
/// measure record, so it is exactly as secret as the case it hangs on. Served inline because the only allowed
/// types are images and the text renders them in place.
/// </remarks>
public static class TextImageFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseTextImageFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/textbilder");

        group.MapGet("/{id}", async (
            string id,
            [FromServices] ITextImageService images,
            [FromServices] ITextImageStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // one 404 for "gone" and "not yours", like every other file endpoint
            var access = await images.GetAccessAsync(id, http.User, cancellationToken);
            if (access is null)
            {
                return Results.NotFound();
            }

            Stream stream;
            try
            {
                stream = storage.OpenRead(access.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            return Results.File(stream, access.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent, Policies.InternalAgent);

        return group;
    }
}
