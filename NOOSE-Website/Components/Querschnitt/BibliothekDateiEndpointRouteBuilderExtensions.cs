using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Querschnitt;

/// <summary>
/// Geschützter Download-Endpoint der Datei-Bibliothek (Phase 7). Dateien liegen außerhalb von
/// wwwroot und werden nur an eingeloggte Agenten ausgeliefert; Verschlusssachen nur an die Führung.
/// Auslieferung ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class BibliothekDateiEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseBibliothekDateiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/bibliothek");

        group.MapGet("/{dateiId}", async (
            string dateiId,
            [FromServices] IBibliothekService bibliothek,
            [FromServices] IBibliothekStorageService storage,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // Der Dienst prüft Soft-Delete und Verschlusssache – liefert sonst null (kein Existenz-Leak).
            var datei = await bibliothek.GetFuerDownloadAsync(dateiId, http.User.IstFuehrung(), cancellationToken);
            if (datei is null)
            {
                return Results.NotFound();
            }

            // Datei öffnen, bevor protokolliert wird; fehlt sie physisch, sauber 404 statt 500.
            Stream stream;
            try
            {
                stream = storage.OeffnenLesen(datei.DateinameGespeichert);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await zugriff.LogAnsichtAsync(nameof(BibliothekDatei), dateiId, cancellationToken);

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, datei.ContentType, datei.OriginalName, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.AktiverAgent);

        return group;
    }
}
