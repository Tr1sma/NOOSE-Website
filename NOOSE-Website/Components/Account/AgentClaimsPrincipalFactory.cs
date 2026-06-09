using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities;

namespace NOOSE_Website.Components.Account;

/// <summary>
/// Schreibt beim Login die NOOSE-spezifischen Claims (Dienstgrad, Status, TRU, Admin, Anzeigename)
/// in das Identity-Cookie. Dadurch entscheiden Policies und UI rein aus den Claims – ohne DB-Zugriff
/// pro Anfrage. Aenderungen an Rang/Status erzwingen ueber den SecurityStamp einen erneuten Login,
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

        identity.AddClaim(new Claim(AgentClaimTypes.Anzeigename, user.Anzeigename ?? string.Empty));
        identity.AddClaim(new Claim(AgentClaimTypes.Status, user.Status.ToString()));
        identity.AddClaim(new Claim(AgentClaimTypes.IstAdmin, user.IstAdmin ? "true" : "false"));
        identity.AddClaim(new Claim(AgentClaimTypes.IstTRU, user.IstTRU ? "true" : "false"));

        if (user.Dienstgrad is not null)
        {
            identity.AddClaim(new Claim(AgentClaimTypes.Dienstgrad, ((int)user.Dienstgrad.Value).ToString()));
        }

        return identity;
    }
}
