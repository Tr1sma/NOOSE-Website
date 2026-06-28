using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>Protected download endpoint for source attachments; classified ones for leadership only, served by stored filename.</summary>
public static class SourcesFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseSourcesFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/quellen");

        group.MapGet("/{sourceId}", async (
            string sourceId,
            [FromQuery] bool inline,
            [FromServices] ISourceService sourceService,
            [FromServices] ISourcesStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // null on no visibility, avoids existence leak
            var source = await sourceService.GetForDownloadAsync(sourceId, ViewerScope.From(http.User), cancellationToken);
            if (source?.FileNameSaved is null)
            {
                return Results.NotFound();
            }

            // open before logging
            Stream stream;
            try
            {
                stream = storage.OpenRead(source.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await access.LogViewAsync(nameof(Source), sourceId, cancellationToken);

            // Results.File disposes the stream, no using
            // inline only for images; everything else stays a download
            var isImage = source.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            return inline && isImage
                ? Results.File(stream, source.ContentType!, enableRangeProcessing: true)
                : Results.File(stream, source.ContentType ?? "application/octet-stream",
                    source.OriginalName, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
