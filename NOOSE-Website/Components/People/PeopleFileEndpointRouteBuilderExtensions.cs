using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.People;

/// <summary>Protected photo endpoints; classified only for leadership.</summary>
public static class PeopleFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNoosePeopleFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/personen");

        group.MapGet("/foto/{fotoId}", async (
            string photoId,
            [FromServices] IPersonService personService,
            [FromServices] IFileStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var photo = await personService.GetPhotoWithPersonAsync(photoId, ViewerScope.From(http.User), cancellationToken);
            // not found / not visible to viewer (partner-gated in the service)
            if (photo?.Person is null)
            {
                return Results.NotFound();
            }

            // open before logging
            Stream stream;
            try
            {
                stream = storage.OpenRead(photo.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await access.LogViewAsync(nameof(PersonPhoto), photoId, cancellationToken);

            // auto-disposed
            return Results.File(stream, photo.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
