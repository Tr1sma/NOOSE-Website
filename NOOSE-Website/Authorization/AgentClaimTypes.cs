namespace NOOSE_Website.Authorization;

/// <summary>
/// Eigene Claim-Typen, die der <c>AgentClaimsPrincipalFactory</c> beim Login in das Cookie
/// schreibt. So können Policies und UI rein aus den Claims entscheiden – ohne DB-Zugriff pro
/// Anfrage.
/// </summary>
public static class AgentClaimTypes
{
    public const string Rank = "noose:dienstgrad";
    public const string Status = "noose:status";
    public const string IsTRU = "noose:tru";
    public const string IsHRB = "noose:hrb";
    public const string IsAdmin = "noose:admin";
    public const string IsTeamLead = "noose:teamleitung";
    public const string Codename = "noose:codename";
    public const string BadgeNumber = "noose:dienstnummer";
    public const string PartnerAgency = "noose:partnerbehoerde";
    public const string PartnerRank = "noose:partnerrang";
}
