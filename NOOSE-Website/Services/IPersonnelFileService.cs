using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Personnel file per agent: rank history (read), notes, and promotion requests. Rank changes live in the agent management service (SecurityStamp/UserManager).</summary>
public interface IPersonnelFileService
{
    Task<List<AgentRankHistory>> GetRankHistoryAsync(string agentId, CancellationToken cancellationToken = default);

    Task<List<AgentNote>> GetNotesAsync(string agentId, AgentNoteKind kind, CancellationToken cancellationToken = default);
    /// <summary>Create a note (commendation/disciplinary) - leadership only.</summary>
    Task<AgentNote> NoteCreateAsync(string agentId, AgentNoteKind kind, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Delete a note - author or leadership.</summary>
    Task NoteDeleteAsync(string noteId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<AgentPromotionRequest>> GetPromotionRequestsAsync(string agentId, CancellationToken cancellationToken = default);
    /// <summary>Open promotion requests (for the inbox + counter).</summary>
    Task<List<AgentPromotionRequest>> GetOpenPromotionRequestsAsync(CancellationToken cancellationToken = default);
    /// <summary>Request a promotion - leadership only; one open request per agent.</summary>
    Task<AgentPromotionRequest> PromotionRequestAsync(string agentId, Rank targetRank, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
