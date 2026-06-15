using System.Security.Claims;
using MudBlazor;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Models.Factions;
using NOOSE_Website.Models.Groups;
using NOOSE_Website.Models.People;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Pages.People.Shared;

/// <summary>
/// Trägt – aus den Dok-Dialogen heraus – eine Person als aktives Mitglied der im Dok verknüpften
/// Fraktion/Personengruppe ein, sofern „Als Mitglied eintragen" gewählt und eine Akte verknüpft wurde.
/// Eine bereits bestehende Mitgliedschaft wird übersprungen; ein Fehler bleibt folgenlos für das
/// bereits gespeicherte Dok (nur Snackbar-Hinweis).
/// </summary>
public static class DocMembership
{
    public static async Task EnterAsync(
        IFactionService factionService,
        IPersonGroupService groupService,
        IPersonService personService,
        ISnackbar snackbar,
        string personId,
        PersonDocInput input,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // Nur bei aktiver Checkbox und verknüpfter Akte (kein Eintrag aus reinem Freitext).
        if (!input.AsMember || string.IsNullOrWhiteSpace(input.OrgId) || string.IsNullOrWhiteSpace(input.OrgType))
        {
            return;
        }

        try
        {
            // Doppelte Mitgliedschaft vermeiden – sonst wirft der jeweilige Dienst.
            var existing = await personService.GetAffiliationsAsync(personId, actor.IsLeadership(), cancellationToken);
            if (existing.Any(z => z.Type == input.OrgType && z.Id == input.OrgId))
            {
                snackbar.Add("Person ist bereits Mitglied.", Severity.Info);
                return;
            }

            if (input.OrgType == nameof(Faction))
            {
                await factionService.MemberAddAsync(input.OrgId, new MemberInput { PersonId = personId }, actor, cancellationToken);
            }
            else
            {
                await groupService.MemberAddAsync(input.OrgId, new GroupMemberInput { PersonId = personId }, actor, cancellationToken);
            }
            snackbar.Add("Person als Mitglied eingetragen.", Severity.Success);
        }
        catch (Exception ex)
        {
            // Das Dok ist bereits gespeichert; die Mitgliedschaft ist nur ein Zusatz – Fehler nicht eskalieren.
            snackbar.Add($"Mitgliedschaft nicht eingetragen: {ex.Message}", Severity.Warning);
        }
    }
}
