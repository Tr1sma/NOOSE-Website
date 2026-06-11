namespace NOOSE_Website.Authorization;

/// <summary>
/// Eigene Claim-Typen, die der <c>AgentClaimsPrincipalFactory</c> beim Login in das Cookie
/// schreibt. So können Policies und UI rein aus den Claims entscheiden – ohne DB-Zugriff pro
/// Anfrage.
/// </summary>
public static class AgentClaimTypes
{
    public const string Dienstgrad = "noose:dienstgrad";
    public const string Status = "noose:status";
    public const string IstTRU = "noose:tru";
    public const string IstAdmin = "noose:admin";
    public const string IstTeamLeitung = "noose:teamleitung";
    public const string Codename = "noose:codename";
    public const string Dienstnummer = "noose:dienstnummer";
}
