using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Querschnitt;

/// <summary>
/// Geschützter Download-Endpoint für Quellen-Anhänge. Dateien liegen außerhalb von wwwroot und werden
/// nur an eingeloggte Agenten ausgeliefert; Anhänge an Verschlusssachen nur an die Führung. Auslieferung
/// erfolgt ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class QuellenDateiEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseQuellenDateiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/quellen");

        group.MapGet("/{quelleId}", async (
            string quelleId,
            [FromServices] IQuelleService quelleService,
            [FromServices] IQuellenStorageService storage,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // Der Dienst prüft Typ=Upload, Soft-Delete und die Sichtbarkeit der Eltern-Akte
            // (Verschlusssache/Papierkorb) – liefert sonst null („nicht vorhanden", kein Existenz-Leak).
            var quelle = await quelleService.GetFuerDownloadAsync(quelleId, http.User.IstFuehrung(), cancellationToken);
            if (quelle?.DateinameGespeichert is null)
            {
                return Results.NotFound();
            }

            // Datei öffnen, bevor protokolliert wird; fehlt sie physisch, sauber 404 statt 500.
            Stream stream;
            try
            {
                stream = storage.OeffnenLesen(quelle.DateinameGespeichert);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await zugriff.LogAnsichtAsync(nameof(Quelle), quelleId, cancellationToken);

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, quelle.ContentType ?? "application/octet-stream",
                quelle.OriginalName, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.AktiverAgent);

        return group;
    }
}
