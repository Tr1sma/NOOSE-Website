using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Fraktionen;

/// <summary>
/// Geschützte Datei-Endpoints der Fraktions-Akten. Fotos liegen außerhalb von wwwroot und werden nur
/// an eingeloggte Agenten ausgeliefert; Fotos von Verschlusssachen nur an die Führung. Auslieferung
/// erfolgt ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class FraktionenDateiEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseFraktionenDateiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/fraktionen");

        group.MapGet("/foto/{fotoId}", async (
            string fotoId,
            [FromServices] IFraktionService fraktionService,
            [FromServices] IFraktionFotoStorageService storage,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var foto = await fraktionService.GetFotoMitFraktionAsync(fotoId, cancellationToken);
            // Nicht gefunden ODER zugehörige Fraktion im Papierkorb (Query-Filter liefert dann null).
            if (foto?.Fraktion is null)
            {
                return Results.NotFound();
            }

            // Verschlusssache: für Nicht-Führung wie „nicht vorhanden" behandeln (kein Existenz-Leak).
            if (foto.Fraktion.IstVerschlusssache && !http.User.IstFuehrung())
            {
                return Results.NotFound();
            }

            // Datei öffnen, bevor protokolliert wird; fehlt sie physisch (z. B. manuell entfernt), sauber 404.
            Stream stream;
            try
            {
                stream = storage.OeffnenLesen(foto.DateinameGespeichert);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await zugriff.LogAnsichtAsync(nameof(FraktionFoto), fotoId, cancellationToken);

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, foto.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.AktiverAgent);

        return group;
    }
}
