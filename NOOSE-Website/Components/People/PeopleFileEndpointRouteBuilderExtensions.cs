using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.People;

/// <summary>
/// Geschützte Datei-Endpoints der Personen-Akten. Fotos liegen außerhalb von wwwroot und werden nur
/// an eingeloggte Agenten ausgeliefert; Fotos von Verschlusssachen nur an die Führung. Auslieferung
/// erfolgt ausschließlich über den in der DB gespeicherten Dateinamen (kein User-Pfad).
/// </summary>
public static class PeopleFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNoosePeopleFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/personen");

        group.MapGet("/foto/{fotoId}", async (
            string photoId,
            [FromServices] IPersonService personService,
            [FromServices] IFileStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var photo = await personService.GetPhotoWithPersonAsync(photoId, cancellationToken);
            // Nicht gefunden ODER zugehörige Person im Papierkorb (Query-Filter liefert dann null).
            if (photo?.Person is null)
            {
                return Results.NotFound();
            }

            // Verschlusssache: für Nicht-Führung wie „nicht vorhanden" behandeln (kein Existenz-Leak).
            if (photo.Person.IsClassified && !http.User.IsLeadership())
            {
                return Results.NotFound();
            }

            // Datei öffnen, bevor protokolliert wird; fehlt sie physisch (z. B. manuell entfernt), sauber 404.
            Stream stream;
            try
            {
                stream = storage.OpenRead(photo.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await access.LogViewAsync(nameof(PersonPhoto), photoId, cancellationToken);

            // Results.File übernimmt und entsorgt den Stream nach dem Senden (kein using!).
            return Results.File(stream, photo.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
