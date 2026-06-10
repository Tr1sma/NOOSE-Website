using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personal;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Personalakte je Agent (Phase 5e): Dienstgrad-Verlauf (lesen), Vermerke (Belobigungen/Disziplinarisches,
/// pflegen durch Führung) und Beförderungsanträge (beantragen durch Führung, entscheiden im
/// <see cref="IAgentVerwaltungService"/>). Rang-Änderungen + Verlauf-Schreiben liegen bewusst im
/// AgentVerwaltungService (SecurityStamp/UserManager); dieser Dienst nutzt das normale Factory-Muster.
/// </summary>
public interface IPersonalakteService
{
    Task<List<AgentDienstgradVerlauf>> GetDienstgradVerlaufAsync(string agentId, CancellationToken cancellationToken = default);

    Task<List<AgentVermerk>> GetVermerkeAsync(string agentId, AgentVermerkArt art, CancellationToken cancellationToken = default);
    /// <summary>Vermerk (Belobigung/Disziplinarisch) anlegen – nur Führung.</summary>
    Task<AgentVermerk> VermerkErstellenAsync(string agentId, AgentVermerkArt art, string text, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    /// <summary>Vermerk löschen – Verfasser oder Führung.</summary>
    Task VermerkLoeschenAsync(string vermerkId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task<List<AgentBefoerderungsantrag>> GetBefoerderungsantraegeAsync(string agentId, CancellationToken cancellationToken = default);
    /// <summary>Offene Beförderungsanträge (für den Posteingang + Zähler).</summary>
    Task<List<AgentBefoerderungsantrag>> GetOffeneBefoerderungsantraegeAsync(CancellationToken cancellationToken = default);
    /// <summary>Beförderung beantragen (Ziel-Dienstgrad + Begründung) – nur Führung; je Agent nur ein offener Antrag.</summary>
    Task<AgentBefoerderungsantrag> BefoerderungBeantragenAsync(string agentId, Dienstgrad zielDienstgrad, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
