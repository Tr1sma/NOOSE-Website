using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>
/// Geschützter Download-Endpoint der Datei-Bibliothek (Phase 7). Dateien liegen außerhalb von
/// wwwroot und werden nur an eingeloggte Agenten ausgeliefert; Verschlusssachen nur an die Führung.
/// Auslieferung ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class LibraryFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseLibraryFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/bibliothek");

        group.MapGet("/{dateiId}", async (
            string fileId,
            [FromServices] ILibraryService library,
            [FromServices] ILibraryStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // Der Dienst prüft Soft-Delete und Verschlusssache – liefert sonst null (kein Existenz-Leak).
            var file = await library.GetForDownloadAsync(fileId, DocumentViewerScope.From(http.User), cancellationToken);
            if (file is null)
            {
                return Results.NotFound();
            }

            // Datei öffnen, bevor protokolliert wird; fehlt sie physisch, sauber 404 statt 500.
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

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, file.ContentType, file.OriginalName, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
