using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Parties;

/// <summary>Protected party photo endpoints; classified only for the record's audience.</summary>
public static class PartiesFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNoosePartiesFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/parteien");

        group.MapGet("/foto/{photoId}", async (
            string photoId,
            [FromServices] IPartyService partyService,
            [FromServices] IPartyPhotoStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var photo = await partyService.GetPhotoWithPartyAsync(photoId, ViewerScope.From(http.User), cancellationToken);
            // not found / not visible to viewer (partner-gated in the service)
            if (photo?.Party is null)
            {
                return Results.NotFound();
            }

            Stream stream;
            try
            {
                stream = storage.OpenRead(photo.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await access.LogViewAsync(nameof(PartyPhoto), photoId, cancellationToken);

            return Results.File(stream, photo.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
