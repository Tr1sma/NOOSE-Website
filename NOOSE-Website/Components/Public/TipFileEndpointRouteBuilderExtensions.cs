using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Components.Public;

/// <summary>Authorized delivery of citizen tip attachments (the submitting citizen or an internal agent).</summary>
/// <remarks>
/// Not anonymous and not under a public route prefix, unlike the wanted photo: a tip attachment is evidence someone
/// handed the agency in confidence, and the only people who may see it are its author and the desk.
/// </remarks>
public static class TipFileEndpointRouteBuilderExtensions
{
    /// <summary>Fixed-window policy name; the submission path is guarded in the service, not here.</summary>
    public const string TipRateLimitPolicy = "noose-hinweis";

    public static IEndpointConventionBuilder MapNooseTipFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/hinweise");

        // the citizen's own, addressed by case number: their projection carries no row id, so the id route below
        // is reachable only from the desk. Same single 404 for every miss.
        group.MapGet("/az/{caseNumber}", async (
            string caseNumber,
            [FromQuery] bool? inline,
            [FromServices] ITipService tips,
            [FromServices] ITipAttachmentStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var access = await tips.GetOwnAttachmentAsync(caseNumber, http.User, cancellationToken);
            return access is null ? Results.NotFound() : Stream(access, storage, inline);
        }).RequireAuthorization().RequireRateLimiting(TipRateLimitPolicy);

        group.MapGet("/{tipId}", async (
            string tipId,
            [FromQuery] bool? inline,
            [FromServices] ITipService tips,
            [FromServices] ITipAttachmentStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var access = await tips.GetAttachmentAsync(tipId, http.User, cancellationToken);
            if (access is null)
            {
                return Results.NotFound();
            }

            return Stream(access, storage, inline);
        }).RequireAuthorization().RequireRateLimiting(TipRateLimitPolicy);

        return group;
    }

    /// <summary>Streams one attachment; shared so both routes answer a missing file the same way.</summary>
    private static IResult Stream(TipAttachmentAccess access, ITipAttachmentStorageService storage, bool? inline)
    {
        System.IO.Stream stream;
        try
        {
            stream = storage.OpenRead(access.FileNameSaved);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound();
        }

        // auto-disposed; inline only for images, everything else stays a download
        var isImage = access.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
        return inline == true && isImage
            ? Results.File(stream, access.ContentType!, enableRangeProcessing: true)
            : Results.File(stream, access.ContentType ?? "application/octet-stream",
                access.OriginalName, enableRangeProcessing: true);
    }
}
