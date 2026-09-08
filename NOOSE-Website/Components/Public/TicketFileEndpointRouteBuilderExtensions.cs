using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Components.Public;

/// <summary>Authorized delivery of ticket attachments (the citizen who holds the ticket, or an agent on it).</summary>
/// <remarks>
/// Not <c>InternalAgent</c>-gated, unlike the text-image endpoint: the citizen has to reach the file they sent and
/// the one the agency sent back. Which of the two routes a caller can use is the whole gate — the case-number route
/// resolves only against the caller's own profile, and the message-id route asks <c>TicketVisibility</c>.
/// </remarks>
public static class TicketFileEndpointRouteBuilderExtensions
{
    /// <summary>Fixed-window policy name; the upload itself is guarded in the service, not here.</summary>
    public const string TicketRateLimitPolicy = "noose-ticket";

    public static IEndpointConventionBuilder MapNooseTicketFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/tickets");

        // the citizen's own, addressed by case number and the position in their thread: their projection carries no
        // row id, and handing one out would tell them a row exists
        group.MapGet("/az/{caseNumber}/{index:int}", async (
            string caseNumber,
            int index,
            [FromQuery] bool? inline,
            [FromServices] ITicketService tickets,
            [FromServices] ITicketAttachmentStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var access = await tickets.GetOwnAttachmentAsync(caseNumber, index, http.User, cancellationToken);
            return access is null ? Results.NotFound() : Stream(access, storage, inline);
        }).RequireAuthorization().RequireRateLimiting(TicketRateLimitPolicy);

        group.MapGet("/{messageId}", async (
            string messageId,
            [FromQuery] bool? inline,
            [FromServices] ITicketService tickets,
            [FromServices] ITicketAttachmentStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var access = await tickets.GetAttachmentAsync(messageId, http.User, cancellationToken);
            return access is null ? Results.NotFound() : Stream(access, storage, inline);
        }).RequireAuthorization().RequireRateLimiting(TicketRateLimitPolicy);

        return group;
    }

    /// <summary>Streams one attachment; shared so both routes answer a missing file the same way.</summary>
    private static IResult Stream(TicketAttachmentAccess access, ITicketAttachmentStorageService storage, bool? inline)
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
