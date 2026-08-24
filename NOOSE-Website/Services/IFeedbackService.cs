using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Feedback;
using FeedbackEntity = NOOSE_Website.Data.Entities.Feedback.Feedback;

namespace NOOSE_Website.Services;

/// <summary>Agent feedback about the website itself; every internal agent reads all of it.</summary>
public interface IFeedbackService
{
    /// <summary>Files a feedback entry; returns the new id.</summary>
    Task<string> CreateAsync(FeedbackInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Caller's own entries, newest first.</summary>
    Task<IReadOnlyList<FeedbackRow>> GetMyAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>All entries of every agent, newest first; open to every internal agent, never to partners.</summary>
    Task<IReadOnlyList<FeedbackRow>> GetInboxAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Sets status and the reply to the reporter; any status may follow any other. Leadership only.</summary>
    Task SetStatusAsync(string id, FeedbackStatus status, string? response, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<FeedbackEntity>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
