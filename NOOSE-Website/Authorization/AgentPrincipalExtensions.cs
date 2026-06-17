using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>Typed access to the NOOSE claims of a signed-in agent.</summary>
public static class AgentPrincipalExtensions
{
    /// <summary>Identity key (GUID) of the agent, or null if not signed in.</summary>
    public static string? GetAgentId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Codename, visible to all users.</summary>
    public static string? GetCodename(this ClaimsPrincipal user)
        => user.FindFirstValue(AgentClaimTypes.Codename);

    /// <summary>Badge number, visible to all users.</summary>
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

    /// <summary>TeamLead marker (FiveM supervision); on its own grants nothing, only forms read-only supervision when combined with no admin flag.</summary>
    public static bool IsTeamLead(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IsTeamLead), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Read-only supervision = TeamLead without admin: reads everything but writes nothing and never sees real names.</summary>
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

    /// <summary>Leadership = rank ≥ Supervisory Special Agent or admin.</summary>
    public static bool IsLeadership(this ClaimsPrincipal user)
        => user.IsAdmin() || user.GetRank() is >= Rank.SupervisorySpecialAgent;

    /// <summary>May READ classified content = leadership or read-only supervision. Read gates only, never write/real-name.</summary>
    public static bool MayClassifiedRead(this ClaimsPrincipal user)
        => user.IsLeadership() || user.IsOnlyReader();

    /// <summary>May see ALL taskforces (without assignment) = leadership or read-only supervision. Sole source of this rule.</summary>
    public static bool MayAllTaskforcesSee(this ClaimsPrincipal user)
        => user.IsLeadership() || user.IsOnlyReader();

    /// <summary>May see the otherwise-hidden real name = leadership/admin but never read-only supervision. Sole source of the real-name rule.</summary>
    public static bool MayRealNameSee(this ClaimsPrincipal user) => user.IsLeadership() && !user.IsOnlyReader();

    /// <summary>May set "secured state-threatening" directly = rank ≥ Senior Special Agent or admin.</summary>
    public static bool MayHighestClassification(this ClaimsPrincipal user)
        => user.IsAdmin() || user.GetRank() is >= Rank.SeniorSpecialAgent;

    /// <summary>May decide promotions = rank ≥ Deputy Director or admin.</summary>
    public static bool MayPromotionDecide(this ClaimsPrincipal user)
        => user.IsAdmin() || user.GetRank() is >= Rank.DeputyDirector;
}
