using System.Security.Claims;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Generic request/inbox workflow. Currently: upgrade requests to "secured state-threatening". Decider = Senior Special Agent+.</summary>
public interface IRequestService
{
    /// <summary>True if an open request already exists for the target record.</summary>
    Task<bool> HasOpenRequestAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    /// <summary>Files an upgrade request for a visible record (justification required).</summary>
    Task UpgradeRequestAsync(string targetType, string targetId, string targetDesignation, Classification target,
        string justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Open requests for the inbox - only those whose target record is visible to the viewer.</summary>
    Task<List<Request>> GetOpenAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Count of open requests visible to the viewer (nav badge).</summary>
    Task<int> GetOpenCountAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>An agent's own requests (open + decided) for the profile view.</summary>
    Task<List<Request>> GetMyAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Decides a request; on approval sets the target's classification and logs it with the request reference.</summary>
    Task DecideAsync(string requestId, bool approved, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}
