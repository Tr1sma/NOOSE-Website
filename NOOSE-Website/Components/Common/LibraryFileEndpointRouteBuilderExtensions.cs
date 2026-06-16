using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>Protected library file endpoint; classified only for leadership.</summary>
public static class LibraryFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseLibraryFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/bibliothek");

        group.MapGet("/{fileId}", async (
            string fileId,
            [FromServices] ILibraryService library,
            [FromServices] ILibraryStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // service checks visibility
            var file = await library.GetForDownloadAsync(fileId, DocumentViewerScope.From(http.User), cancellationToken);
            if (file is null)
            {
                return Results.NotFound();
            }

            // open before logging
            Stream stream;
            try
            {
                stream = storage.OpenRead(file.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await access.LogViewAsync(nameof(LibraryFile), fileId, cancellationToken);

            // auto-disposed
            return Results.File(stream, file.ContentType, file.OriginalName, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent, Policies.InternalAgent);

        return group;
    }
}
