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

    /// <summary>Führung = Dienstgrad ≥ Supervisory Special Agent oder Admin.</summary>
    public static bool IstFuehrung(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.SupervisorySpecialAgent;

    /// <summary>
    /// Darf den (sonst verborgenen) Klarnamen sehen = Führungsebene oder Admin. Einzige Quelle der
    /// Klarname-Sichtbarkeitsregel – überall hierüber prüfen, statt Dienstgrad/Admin einzeln abzufragen.
    /// </summary>
    public static bool DarfKlarnameSehen(this ClaimsPrincipal user) => user.IstFuehrung();

    /// <summary>Darf „Gesichert staatsgefährdend" direkt setzen = Dienstgrad ≥ Senior Special Agent oder Admin.</summary>
    public static bool DarfHoechsteEinstufung(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.SeniorSpecialAgent;

    /// <summary>Darf Beförderungen entscheiden = Dienstgrad ≥ Deputy Director oder Admin (entspricht <c>Policies.BefoerderungEntscheiden</c>).</summary>
    public static bool DarfBefoerderungEntscheiden(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.DeputyDirector;
}
