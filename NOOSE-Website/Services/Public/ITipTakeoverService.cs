using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Public;

/// <summary>Turns a citizen tip into internal records and links them back to it.</summary>
/// <remarks>
/// An orchestrator, pattern of <c>ApplicationCaseService</c>: it calls the existing record services and builds no
/// entity by hand, so their classification gates and case-number transactions keep working. Every takeover ends in a
/// manual link, which is what makes the origin visible on the file's timeline.
/// </remarks>
public interface ITipTakeoverService
{
    /// <summary>What this tip has already been turned into, as the link panel would show it.</summary>
    Task<IReadOnlyList<LinkDisplay>> GetStateAsync(string tipId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a fresh person file for the tip and links it; returns the new person id.</summary>
    Task<string> ToNewPersonAsync(string tipId, string name, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Links the tip to a person file that already exists.</summary>
    Task AttachPersonAsync(string tipId, string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a case for the tip and links it; returns the new case id.</summary>
    Task<string> ToCaseAsync(string tipId, string? title, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Files the tip as an observation on a person file and links it; returns the observation id.</summary>
    Task<string> ToObservationAsync(string tipId, string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}
