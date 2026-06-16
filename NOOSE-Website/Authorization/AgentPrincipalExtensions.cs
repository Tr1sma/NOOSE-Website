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
    public static string? GetBadgeNumber(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AgentClaimTypes.BadgeNumber);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static Rank? GetRank(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AgentClaimTypes.Rank);
        return int.TryParse(raw, out var value) && Enum.IsDefined(typeof(Rank), value)
            ? (Rank)value
            : null;
    }

    public static AgentStatus? GetStatus(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AgentClaimTypes.Status);
        return Enum.TryParse<AgentStatus>(raw, out var value) ? value : null;
    }

    public static bool IsAdmin(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IsAdmin), "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsTRU(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IsTRU), "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsHRB(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IsHRB), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>TeamLeitung-Marker (FiveM-Aufsicht) aus dem Claim. Für sich genommen keine Rolle – erst in
    /// Kombination mit dem fehlenden Admin-Haken ergibt sich die Nur-Lese-Aufsicht (<see cref="IstNurLeser"/>).</summary>
    public static bool IsTeamLead(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IsTeamLead), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Globale Nur-Lese-Aufsicht = TeamLeitung OHNE Admin. Darf alles lesen (inkl. aller Seiten und
    /// Verschlusssachen, siehe <see cref="DarfVerschlusssacheLesen"/>), aber NICHTS schreiben
    /// (<see cref="DarfSchreiben"/>) und KEINE Klarnamen sehen (<see cref="DarfKlarnameSehen"/>).
    /// Ein TeamLeiter MIT Admin-Haken ist kein Nur-Leser und behält vollen Zugriff.
    /// </summary>
    public static bool IsOnlyReader(this ClaimsPrincipal user)
        => user.IsTeamLead() && !user.IsAdmin();

    /// <summary>External partner agency from claim, or null for internal NOOSE agents.</summary>
    public static PartnerAgency? GetPartnerAgency(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AgentClaimTypes.PartnerAgency);
        return int.TryParse(raw, out var value) && Enum.IsDefined(typeof(PartnerAgency), value)
            ? (PartnerAgency)value
            : null;
    }

    /// <summary>Partner rank tier from claim, or null for internal NOOSE agents.</summary>
    public static PartnerRank? GetPartnerRank(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AgentClaimTypes.PartnerRank);
        return int.TryParse(raw, out var value) && Enum.IsDefined(typeof(PartnerRank), value)
            ? (PartnerRank)value
            : null;
    }

    /// <summary>External partner (DoJ/LSPD/LSMD): read-only, sees only released non-classified content.</summary>
    public static bool IsPartner(this ClaimsPrincipal user) => user.GetPartnerAgency() is not null;

    /// <summary>True if the viewer is a partner of the given agency at or above the given rank tier.</summary>
    public static bool HasPartnerRank(this ClaimsPrincipal user, PartnerAgency agency, PartnerRank minimum)
        => user.GetPartnerAgency() == agency && user.GetPartnerRank() is { } rank && rank >= minimum;

    /// <summary>May write at all; false for read-only supervisors and partners. Sole source for write-control visibility.</summary>
    public static bool MayWrite(this ClaimsPrincipal user) => !user.IsOnlyReader() && !user.IsPartner();

    /// <summary>Führung = Dienstgrad ≥ Supervisory Special Agent oder Admin.</summary>
    public static bool IsLeadership(this ClaimsPrincipal user)
        => user.IsAdmin() || user.GetRank() is >= Rank.SupervisorySpecialAgent;

    /// <summary>
    /// Darf Verschlusssachen-Inhalte LESEN = Führung oder Nur-Lese-Aufsicht. Ausschließlich für Lese-Gates
    /// (Sichtbarkeit von VS-Akten) verwenden – NIE für Schreiben oder Klarname-Sicht (die Aufsicht darf VS
    /// sehen, aber keine Klarnamen).
    /// </summary>
    public static bool MayClassifiedRead(this ClaimsPrincipal user)
        => user.IsLeadership() || user.IsOnlyReader();

    /// <summary>Darf ALLE Taskforces sehen (auch ohne Zuteilung) = Führung/Admin oder Nur-Lese-Aufsicht. Sonst
    /// sieht ein Agent nur die Taskforces, denen er zugeteilt ist. Einzige Quelle dieser Regel.</summary>
    public static bool MayAllTaskforcesSee(this ClaimsPrincipal user)
        => user.IsLeadership() || user.IsOnlyReader();

    /// <summary>
    /// Darf den (sonst verborgenen) Klarnamen sehen = Führungsebene oder Admin, ABER nie die Nur-Lese-Aufsicht.
    /// Einzige Quelle der Klarname-Sichtbarkeitsregel – überall hierüber prüfen, statt Dienstgrad/Admin einzeln
    /// abzufragen. Nur-Leser sehen trotz „alles lesen" bewusst keine Klarnamen (auch bei hohem Dienstgrad).
    /// </summary>
    public static bool MayRealNameSee(this ClaimsPrincipal user) => user.IsLeadership() && !user.IsOnlyReader();

    /// <summary>Darf „Gesichert staatsgefährdend" direkt setzen = Dienstgrad ≥ Senior Special Agent oder Admin.</summary>
    public static bool MayHighestClassification(this ClaimsPrincipal user)
        => user.IsAdmin() || user.GetRank() is >= Rank.SeniorSpecialAgent;

    /// <summary>Darf Beförderungen entscheiden = Dienstgrad ≥ Deputy Director oder Admin (entspricht <c>Policies.BefoerderungEntscheiden</c>).</summary>
    public static bool MayPromotionDecide(this ClaimsPrincipal user)
        => user.IsAdmin() || user.GetRank() is >= Rank.DeputyDirector;
}
