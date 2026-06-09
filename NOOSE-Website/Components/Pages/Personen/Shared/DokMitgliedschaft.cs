using System.Security.Claims;
using MudBlazor;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Models.Fraktionen;
using NOOSE_Website.Models.Gruppen;
using NOOSE_Website.Models.Personen;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Pages.Personen.Shared;

/// <summary>
/// Trägt – aus den Dok-Dialogen heraus – eine Person als aktives Mitglied der im Dok verknüpften
/// Fraktion/Personengruppe ein, sofern „Als Mitglied eintragen" gewählt und eine Akte verknüpft wurde.
/// Eine bereits bestehende Mitgliedschaft wird übersprungen; ein Fehler bleibt folgenlos für das
/// bereits gespeicherte Dok (nur Snackbar-Hinweis).
/// </summary>
public static class DokMitgliedschaft
{
    public static async Task EintragenAsync(
        IFraktionService fraktionService,
        IPersonengruppeService gruppeService,
        IPersonService personService,
        ISnackbar snackbar,
        string personId,
        PersonDokEingabe eingabe,
        ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        // Nur bei aktiver Checkbox und verknüpfter Akte (kein Eintrag aus reinem Freitext).
        if (!eingabe.AlsMitglied || string.IsNullOrWhiteSpace(eingabe.OrgId) || string.IsNullOrWhiteSpace(eingabe.OrgTyp))
        {
            return;
        }

        try
        {
            // Doppelte Mitgliedschaft vermeiden – sonst wirft der jeweilige Dienst.
            var bestehende = await personService.GetZugehoerigkeitenAsync(personId, handelnder.IstFuehrung(), cancellationToken);
            if (bestehende.Any(z => z.Typ == eingabe.OrgTyp && z.Id == eingabe.OrgId))
            {
                snackbar.Add("Person ist bereits Mitglied.", Severity.Info);
                return;
            }

            if (eingabe.OrgTyp == nameof(Fraktion))
            {
                await fraktionService.MitgliedHinzufuegenAsync(eingabe.OrgId, new MitgliedEingabe { PersonId = personId }, handelnder, cancellationToken);
            }
            else
            {
                await gruppeService.MitgliedHinzufuegenAsync(eingabe.OrgId, new GruppeMitgliedEingabe { PersonId = personId }, handelnder, cancellationToken);
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
