namespace NOOSE_Website.Authorization;

/// <summary>Custom claim types written into the cookie at login, so policies and UI decide from claims without per-request DB hits.</summary>
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
