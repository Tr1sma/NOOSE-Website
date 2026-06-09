using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame Einstufungs-Logik für alle Akten (Person/Fraktion/Personengruppe): das Rang-Gate für
/// „Gesichert staatsgefährdend" und die Erzeugung eines polymorphen Verlauf-Eintrags.
/// </summary>
public static class EinstufungHelfer
{
    /// <summary>Wirft, wenn „Gesichert staatsgefährdend" ohne Senior Special Agent/Admin gesetzt würde.</summary>
    public static void PruefeRangGate(Einstufung neu, ClaimsPrincipal handelnder)
    {
        if (neu == Einstufung.GesichertStaatsgefaehrdend && !handelnder.DarfHoechsteEinstufung())
        {
            throw new InvalidOperationException(
                "'Gesichert staatsgefährdend' darf erst ab Senior Special Agent direkt gesetzt werden – sonst per Antrag (Phase 5).");
        }
    }

    /// <summary>Baut einen (append-only) Verlauf-Eintrag für die Akte <paramref name="entitaetTyp"/>/<paramref name="entitaetId"/>.</summary>
    public static EinstufungVerlauf Eintrag(string entitaetTyp, string entitaetId, Einstufung wert, string? begruendung, ClaimsPrincipal handelnder)
        => new()
        {
            EntitaetTyp = entitaetTyp,
            EntitaetId = entitaetId,
            Wert = wert,
            Begruendung = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim(),
            Zeitpunkt = DateTime.UtcNow,
            AgentId = handelnder.GetAgentId(),
            AgentName = handelnder.GetCodename(),
        };
}
