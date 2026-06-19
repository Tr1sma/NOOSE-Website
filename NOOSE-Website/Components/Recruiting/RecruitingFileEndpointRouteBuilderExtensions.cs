using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Recruiting;

/// <summary>Authorized delivery of application attachments (owner or HRB/leadership only).</summary>
public static class RecruitingFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseRecruitingFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/bewerbungen");

        group.MapGet("/anhang/{bewerbungId}", async (
            string bewerbungId,
            [FromServices] IBewerbungService bewerbungService,
            [FromServices] ISourcesStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var bewerbung = await bewerbungService.GetForFileAccessAsync(bewerbungId, http.User, cancellationToken);
            if (bewerbung?.AttachmentFileNameSaved is null)
            {
                return Results.NotFound();
            }
            Stream stream = storage.OpenRead(bewerbung.AttachmentFileNameSaved);
            return Results.File(stream, bewerbung.AttachmentContentType ?? "application/octet-stream",
                bewerbung.AttachmentOriginalName, enableRangeProcessing: true);
        }).RequireAuthorization();

        return group;
    }
}
