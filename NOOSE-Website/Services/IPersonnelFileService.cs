using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Personalakte je Agent (Phase 5e): Dienstgrad-Verlauf (lesen), Vermerke (Belobigungen/Disziplinarisches,
/// pflegen durch Führung) und Beförderungsanträge (beantragen durch Führung, entscheiden im
/// <see cref="IAgentVerwaltungService"/>). Rang-Änderungen + Verlauf-Schreiben liegen bewusst im
/// AgentVerwaltungService (SecurityStamp/UserManager); dieser Dienst nutzt das normale Factory-Muster.
/// </summary>
public interface IPersonnelFileService
{
    Task<List<AgentRankHistory>> GetRankHistoryAsync(string agentId, CancellationToken cancellationToken = default);

    Task<List<AgentNote>> GetNotesAsync(string agentId, AgentNoteKind kind, CancellationToken cancellationToken = default);
    /// <summary>Vermerk (Belobigung/Disziplinarisch) anlegen – nur Führung.</summary>
    Task<AgentNote> NoteCreateAsync(string agentId, AgentNoteKind kind, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Vermerk löschen – Verfasser oder Führung.</summary>
    Task NoteDeleteAsync(string noteId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<AgentPromotionRequest>> GetPromotionRequestsAsync(string agentId, CancellationToken cancellationToken = default);
    /// <summary>Offene Beförderungsanträge (für den Posteingang + Zähler).</summary>
    Task<List<AgentPromotionRequest>> GetOpenPromotionRequestsAsync(CancellationToken cancellationToken = default);
    /// <summary>Beförderung beantragen (Ziel-Dienstgrad + Begründung) – nur Führung; je Agent nur ein offener Antrag.</summary>
    Task<AgentPromotionRequest> PromotionRequestAsync(string agentId, Rank targetRank, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
