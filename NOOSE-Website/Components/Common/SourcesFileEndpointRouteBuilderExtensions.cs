using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>
/// Geschützter Download-Endpoint für Quellen-Anhänge. Dateien liegen außerhalb von wwwroot und werden
/// nur an eingeloggte Agenten ausgeliefert; Anhänge an Verschlusssachen nur an die Führung. Auslieferung
/// erfolgt ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class SourcesFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseSourcesFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/quellen");

        group.MapGet("/{quelleId}", async (
            string sourceId,
            [FromServices] ISourceService sourceService,
            [FromServices] ISourcesStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // Der Dienst prüft Typ=Upload, Soft-Delete und die Sichtbarkeit der Eltern-Akte
            // (Verschlusssache/Papierkorb) – liefert sonst null („nicht vorhanden", kein Existenz-Leak).
            var source = await sourceService.GetForDownloadAsync(sourceId, http.User.IsLeadership(), cancellationToken, http.User.GetAgentId());
            if (source?.FileNameSaved is null)
            {
                return Results.NotFound();
            }

            // Datei öffnen, bevor protokolliert wird; fehlt sie physisch, sauber 404 statt 500.
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

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, source.ContentType ?? "application/octet-stream",
                source.OriginalName, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
