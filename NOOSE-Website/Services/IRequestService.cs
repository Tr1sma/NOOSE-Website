using System.Security.Claims;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Generischer Antrags-/Posteingang-Workflow (Phase 5). Aktuell: Hochstufungs-Anträge auf die Einstufung
/// „Gesichert staatsgefährdend". Antragsteller = Agent unterhalb Senior Special Agent; Entscheider =
/// Senior Special Agent+ (siehe <see cref="Berechtigung.VerlangeHoechsteEinstufung"/>).
/// </summary>
public interface IRequestService
{
    /// <summary>True, wenn für die Ziel-Akte bereits ein offener (beantragter) Antrag existiert.</summary>
    Task<bool> HasOpenRequestAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    /// <summary>Stellt einen Hochstufungs-Antrag für eine sichtbare Akte (Begründung erforderlich).</summary>
    Task UpgradeRequestAsync(string targetType, string targetId, string targetDesignation, Classification target,
        string justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Offene Anträge für den Posteingang – nur solche, deren Ziel-Akte für den Betrachter sichtbar ist.</summary>
    Task<List<Request>> GetOpenAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Anzahl offener, für den Betrachter sichtbarer Anträge (NavMenu-Badge).</summary>
    Task<int> GetOpenCountAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Eigene Anträge eines Agenten (offen + entschieden) für die Profil-Ansicht.</summary>
    Task<List<Request>> GetMyAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Entscheidet einen Antrag. Bei Genehmigung wird die Einstufung der Ziel-Akte gesetzt
    /// und im Einstufungs-Verlauf mit Antrags-Bezug protokolliert.</summary>
    Task DecideAsync(string requestId, bool approved, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}
