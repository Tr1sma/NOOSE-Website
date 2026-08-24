using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Account;

/// <summary>Serves agent profile pictures; the visibility decision lives in AgentAvatar, not here.</summary>
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
            var owner = await agents.FindByAvatarFileAsync(fileName, cancellationToken);
            // unknown file and a staged one the viewer may not have answer alike
            if (owner is null
                || AgentAvatar.ServableContentType(owner, fileName, http.User) is not { } contentType)
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

            return Results.File(stream, contentType);
        })
        .RequireAuthorization(Policies.ActiveAgent);

        return group;
    }
}
