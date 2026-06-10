using System.Security.Claims;
using NOOSE_Website.Data.Entities.Antraege;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Generischer Antrags-/Posteingang-Workflow (Phase 5). Aktuell: Hochstufungs-Anträge auf die Einstufung
/// „Gesichert staatsgefährdend". Antragsteller = Agent unterhalb Senior Special Agent; Entscheider =
/// Senior Special Agent+ (siehe <see cref="Berechtigung.VerlangeHoechsteEinstufung"/>).
/// </summary>
public interface IAntragService
{
    /// <summary>True, wenn für die Ziel-Akte bereits ein offener (beantragter) Antrag existiert.</summary>
    Task<bool> HatOffenenAntragAsync(string zielTyp, string zielId, CancellationToken cancellationToken = default);

    /// <summary>Stellt einen Hochstufungs-Antrag für eine sichtbare Akte (Begründung erforderlich).</summary>
    Task HochstufungBeantragenAsync(string zielTyp, string zielId, string zielBezeichnung, Einstufung ziel,
        string begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Offene Anträge für den Posteingang – nur solche, deren Ziel-Akte für den Betrachter sichtbar ist.</summary>
    Task<List<Antrag>> GetOffeneAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Anzahl offener, für den Betrachter sichtbarer Anträge (NavMenu-Badge).</summary>
    Task<int> GetOffeneAnzahlAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Eigene Anträge eines Agenten (offen + entschieden) für die Profil-Ansicht.</summary>
    Task<List<Antrag>> GetMeineAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Entscheidet einen Antrag. Bei Genehmigung wird die Einstufung der Ziel-Akte gesetzt
    /// und im Einstufungs-Verlauf mit Antrags-Bezug protokolliert.</summary>
    Task EntscheidenAsync(string antragId, bool genehmigt, string? notiz, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default);
}
