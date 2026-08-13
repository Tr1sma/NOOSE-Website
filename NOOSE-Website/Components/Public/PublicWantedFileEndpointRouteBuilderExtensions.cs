using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Components.Public;

/// <summary>Photo of a published wanted notice — the one anonymous file endpoint in the app.</summary>
/// <remarks>
/// A mugshot only agents can see is not a wanted poster, so this endpoint is deliberately not behind
/// <c>RequireAuthorization</c>. The authorisation is the publication check plus the module gate inside the service,
/// and the file it streams is a copy under the public upload path — an internal photo is not reachable from here.
/// <para>
/// It lives under <c>/gesucht</c> because that prefix is already public to the crawler and to
/// <c>PublicIndexingMiddleware</c>; a separate <c>/dateien/…</c> route would need its own robots entry or the picture
/// of every poster would be blocked.
/// </para>
/// </remarks>
public static class PublicWantedFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNoosePublicWantedFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/gesucht");

        group.MapGet("/{caseNumber}/foto", async (
            string caseNumber,
            [FromServices] IPublicWantedService wanted,
            [FromServices] IPublicWantedPhotoStorageService storage,
            CancellationToken cancellationToken) =>
        {
            // one answer for every miss — unknown, draft, retracted, captured, module off, kill switch, classified
            // file, missing file. Anything that distinguishes them turns the endpoint into an existence oracle.
            var photo = await wanted.GetPublishedPhotoAsync(caseNumber, cancellationToken);
            if (photo is null)
            {
                return Results.NotFound();
            }

            Stream stream;
            try
            {
                stream = storage.OpenRead(photo.FileNameSaved);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
            {
                return Results.NotFound();
            }

            // no access log: an anonymous visitor is nobody, and the row would flood the log with actor-less entries
            return Results.File(stream, photo.ContentType, enableRangeProcessing: true);
        })
        .AllowAnonymous();

        return group;
    }
}
