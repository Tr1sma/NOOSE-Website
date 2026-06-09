using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Bequemer, typsicherer Zugriff auf die NOOSE-Claims eines angemeldeten Agents.
/// Genutzt von Policies, Services und UI-Komponenten.
/// </summary>
public static class AgentPrincipalExtensions
{
    /// <summary>Identity-Schluessel (GUID) des Agents oder null, falls nicht eingeloggt.</summary>
    public static string? GetAgentId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? GetAnzeigename(this ClaimsPrincipal user)
        => user.FindFirstValue(AgentClaimTypes.Anzeigename);

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

    /// <summary>Fuehrung = Dienstgrad ≥ Supervisory Special Agent oder Admin.</summary>
    public static bool IstFuehrung(this ClaimsPrincipal user)
        => user.IstAdmin() || user.GetDienstgrad() is >= Dienstgrad.SupervisorySpecialAgent;
}
