using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Factions;

/// <summary>Protected faction photo endpoints; classified only for leadership.</summary>
public static class FactionsFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseFactionsFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/fraktionen");

        group.MapGet("/foto/{fotoId}", async (
            string photoId,
            [FromServices] IFactionService factionService,
            [FromServices] IFactionPhotoStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var photo = await factionService.GetPhotoWithFactionAsync(photoId, cancellationToken);
            // not found
            if (photo?.Faction is null)
            {
                return Results.NotFound();
            }

            // hide existence
            if (photo.Faction.IsClassified && !http.User.IsLeadership())
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

            await access.LogViewAsync(nameof(FactionPhoto), photoId, cancellationToken);

            // auto-disposed
            return Results.File(stream, photo.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
