using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Evidence;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Common;

/// <summary>Protected evidence-item image endpoint; readable by any active agent.</summary>
public static class EvidenceFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseEvidenceFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/asservate");

        group.MapGet("/{itemId}", async (
            string itemId,
            [FromServices] IEvidenceService evidence,
            [FromServices] IEvidenceImageStorageService storage,
            [FromServices] IAccessLogService access,
            CancellationToken cancellationToken) =>
        {
            var item = await evidence.GetItemAsync(itemId, cancellationToken);
            if (item is null || string.IsNullOrEmpty(item.ImageFileName))
            {
                return Results.NotFound();
            }

            Stream stream;
            try
            {
                stream = storage.OpenRead(item.ImageFileName);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }

            await access.LogViewAsync(nameof(EvidenceItem), itemId, cancellationToken);

            return Results.File(stream, item.ImageContentType ?? "application/octet-stream", enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
