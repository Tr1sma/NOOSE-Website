using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Account;

/// <summary>Serves agent profile pictures; a staged one reaches only its owner and leadership.</summary>
public static class AgentFileEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseAgentFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dateien/agenten");

        group.MapGet("/profilbild/{fileName}", async (
            string fileName,
            [FromServices] IAgentManagementService agents,
            [FromServices] IAgentAvatarStorageService storage,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var agent = await agents.FindByAvatarFileAsync(fileName, cancellationToken);
            if (agent is null)
            {
                return Results.NotFound();
            }

            var released = agent.AvatarFileName == fileName;
            // an unreleased picture is not public yet; leadership must see it to decide
            if (!released && http.User.GetAgentId() != agent.Id && !http.User.IsLeadership())
            {
                return Results.NotFound();
            }

            Stream stream;
            try
            {
                stream = storage.OpenRead(fileName);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InvalidOperationException)
            {
                return Results.NotFound();
            }

            var contentType = (released ? agent.AvatarContentType : agent.PendingAvatarContentType)
                ?? "application/octet-stream";
            return Results.File(stream, contentType);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
