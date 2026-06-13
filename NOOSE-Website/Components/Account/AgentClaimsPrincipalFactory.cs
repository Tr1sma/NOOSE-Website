using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities;

namespace NOOSE_Website.Components.Account;

/// <summary>
/// Schreibt beim Login die NOOSE-spezifischen Claims (Dienstgrad, Status, TRU, HRB, Admin, TeamLeitung, Codename,
/// Dienstnummer) in das Identity-Cookie. Dadurch entscheiden Policies und UI rein aus den Claims – ohne DB-Zugriff
/// pro Anfrage. Änderungen an Rang/Status erzwingen über den SecurityStamp einen erneuten Login,
/// sodass die Claims aktuell bleiben.
/// </summary>
public class AgentClaimsPrincipalFactory(
    UserManager<Agent> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<Agent, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Agent user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(AgentClaimTypes.Codename, user.Codename ?? string.Empty));
        identity.AddClaim(new Claim(AgentClaimTypes.Dienstnummer, user.Dienstnummer ?? string.Empty));
        identity.AddClaim(new Claim(AgentClaimTypes.Status, user.Status.ToString()));
        identity.AddClaim(new Claim(AgentClaimTypes.IstAdmin, user.IstAdmin ? "true" : "false"));
        identity.AddClaim(new Claim(AgentClaimTypes.IstTRU, user.IstTRU ? "true" : "false"));
        identity.AddClaim(new Claim(AgentClaimTypes.IstHRB, user.IstHRB ? "true" : "false"));
        // TeamLeitung ist nun ein Claim: Die Nur-Lese-Aufsichtsrolle (TeamLeitung ohne Admin) entscheidet
        // über Policies/UI rein aus den Claims. Das Umschalten erneuert den SecurityStamp (siehe
        // AgentVerwaltungService.TeamLeitungSetzenAsync), damit der Claim beim nächsten Login aktuell ist.
        identity.AddClaim(new Claim(AgentClaimTypes.IstTeamLeitung, user.IstTeamLeitung ? "true" : "false"));

        if (user.Dienstgrad is not null)
        {
            identity.AddClaim(new Claim(AgentClaimTypes.Dienstgrad, ((int)user.Dienstgrad.Value).ToString()));
        }

        return identity;
    }
}
