using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Bequemer, typsicherer Zugriff auf die NOOSE-Claims eines angemeldeten Agents.
/// Genutzt von Policies, Services und UI-Komponenten.
/// </summary>
public static class AgentPrincipalExtensions
{
    /// <summary>Identity-Schlüssel (GUID) des Agents oder null, falls nicht eingeloggt.</summary>
    public static string? GetAgentId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Codename (Deckname) – der für alle Nutzer sichtbare Name.</summary>
    public static string? GetCodename(this ClaimsPrincipal user)
        => user.FindFirstValue(AgentClaimTypes.Codename);

    /// <summary>Dienstnummer (Freitext) – für alle Nutzer sichtbar, leer wenn nicht vergeben.</summary>
    public static string? GetDienstnummer(this ClaimsPrincipal user)
    {
        var wert = user.FindFirstValue(AgentClaimTypes.Dienstnummer);
        return string.IsNullOrWhiteSpace(wert) ? null : wert;
    }

    public static Dienstgrad? GetDienstgrad(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AgentClaimTypes.Dienstgrad);
        return int.TryParse(raw, out var value) && Enum.IsDefined(typeof(Dienstgrad), value)
            ? (Dienstgrad)value
            : null;
    }

    public static AgentStatus? GetStatus(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AgentClaimTypes.Status);
        return Enum.TryParse<AgentStatus>(raw, out var value) ? value : null;
    }

    public static bool IstAdmin(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IstAdmin), "true", StringComparison.OrdinalIgnoreCase);

    public static bool IstTRU(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IstTRU), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>TeamLeitung-Marker (FiveM-Aufsicht) aus dem Claim. Für sich genommen keine Rolle – erst in
    /// Kombination mit dem fehlenden Admin-Haken ergibt sich die Nur-Lese-Aufsicht (<see cref="IstNurLeser"/>).</summary>
    public static bool IstTeamLeitung(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IstTeamLeitung), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Globale Nur-Lese-Aufsicht = TeamLeitung OHNE Admin. Darf alles lesen (inkl. aller Seiten und
    /// Verschlusssachen, siehe <see cref="DarfVerschlusssacheLesen"/>), aber NICHTS schreiben
    /// (<see cref="DarfSchreiben"/>) und KEINE Klarnamen sehen (<see cref="DarfKlarnameSehen"/>).
    /// Ein TeamLeiter MIT Admin-Haken ist kein Nur-Leser und behält vollen Zugriff.
    /// </summary>
    public static bool IstNurLeser(this ClaimsPrincipal user)
        => user.IstTeamLeitung() && !user.IstAdmin();

    /// <summary>Darf überhaupt Daten schreiben/ändern. Nur-Leser (Aufsicht) dürfen das nie. Einzige Quelle
    /// der Schreib-Sichtbarkeitsregel für die UI – Mutations-Controls hierüber aus-/einblenden.</summary>
    public static bool DarfSchreiben(this ClaimsPrincipal user) => !user.IstNurLeser();

    /// <summary>Führung = Dienstgrad ≥ Supervisory Special Agent oder Admin.</summary>
    public static bool IstFuehrung(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.SupervisorySpecialAgent;

    /// <summary>
    /// Darf Verschlusssachen-Inhalte LESEN = Führung oder Nur-Lese-Aufsicht. Ausschließlich für Lese-Gates
    /// (Sichtbarkeit von VS-Akten) verwenden – NIE für Schreiben oder Klarname-Sicht (die Aufsicht darf VS
    /// sehen, aber keine Klarnamen).
    /// </summary>
    public static bool DarfVerschlusssacheLesen(this ClaimsPrincipal user)
        => user.IstFuehrung() || user.IstNurLeser();

    /// <summary>Darf ALLE Taskforces sehen (auch ohne Zuteilung) = Führung/Admin oder Nur-Lese-Aufsicht. Sonst
    /// sieht ein Agent nur die Taskforces, denen er zugeteilt ist. Einzige Quelle dieser Regel.</summary>
    public static bool DarfAlleTaskforcesSehen(this ClaimsPrincipal user)
        => user.IstFuehrung() || user.IstNurLeser();

    /// <summary>
    /// Darf den (sonst verborgenen) Klarnamen sehen = Führungsebene oder Admin, ABER nie die Nur-Lese-Aufsicht.
    /// Einzige Quelle der Klarname-Sichtbarkeitsregel – überall hierüber prüfen, statt Dienstgrad/Admin einzeln
    /// abzufragen. Nur-Leser sehen trotz „alles lesen" bewusst keine Klarnamen (auch bei hohem Dienstgrad).
    /// </summary>
    public static bool DarfKlarnameSehen(this ClaimsPrincipal user) => user.IstFuehrung() && !user.IstNurLeser();

    /// <summary>Darf „Gesichert staatsgefährdend" direkt setzen = Dienstgrad ≥ Senior Special Agent oder Admin.</summary>
    public static bool DarfHoechsteEinstufung(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.SeniorSpecialAgent;

    /// <summary>Darf Beförderungen entscheiden = Dienstgrad ≥ Deputy Director oder Admin (entspricht <c>Policies.BefoerderungEntscheiden</c>).</summary>
    public static bool DarfBefoerderungEntscheiden(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.DeputyDirector;
}
