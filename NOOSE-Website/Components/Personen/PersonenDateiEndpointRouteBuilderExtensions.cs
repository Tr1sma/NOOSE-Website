using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Personen;

/// <summary>
/// Geschützte Datei-Endpoints der Personen-Akten. Fotos liegen außerhalb von wwwroot und werden nur
/// an eingeloggte Agenten ausgeliefert; Fotos von Verschlusssachen nur an die Führung. Auslieferung
/// erfolgt ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class PersonenDateiEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNoosePersonenDateiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/personen");

        group.MapGet("/foto/{fotoId}", async (
            string fotoId,
            [FromServices] IPersonService personService,
            [FromServices] IFileStorageService storage,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var foto = await personService.GetFotoMitPersonAsync(fotoId, cancellationToken);
            // Nicht gefunden ODER zugehörige Person im Papierkorb (Query-Filter liefert dann null).
            if (foto?.Person is null)
            {
                return Results.NotFound();
            }

            // Verschlusssache: für Nicht-Führung wie „nicht vorhanden" behandeln (kein Existenz-Leak).
            if (foto.Person.IstVerschlusssache && !http.User.IstFuehrung())
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

            await zugriff.LogAnsichtAsync(nameof(PersonFoto), fotoId, cancellationToken);

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, foto.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.AktiverAgent);

        return group;
    }
}
