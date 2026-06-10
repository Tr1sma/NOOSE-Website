using MudBlazor;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Pages.Personen.Shared;

/// <summary>
/// Gemeinsame Dubletten-Prüfung für die Schnellanlage-Pfade (Dok für neue Person, Mitglied „neue Person
/// anlegen"). Zeigt bei gleichnamigen Akten den Warn-Dialog – damit nicht nur das reguläre
/// „Neue Person"-Formular vor Dubletten warnt.
/// </summary>
public static class DuplikatPruefung
{
    /// <summary>
    /// Liefert true, wenn die neue Akte angelegt werden darf: kein gleichnamiger Treffer ODER der Nutzer
    /// hat „Trotzdem anlegen" bestätigt. Bei leerem Namen wird nicht geprüft (true).
    /// </summary>
    public static async Task<bool> DarfNeueAkteAnlegenAsync(
        IDialogService dialog, IPersonService personService, string? neuerName, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(neuerName))
        {
            return true;
        }
        var duplikate = await personService.FindeDuplikateAsync(neuerName.Trim(), Array.Empty<string>(), istFuehrung, cancellationToken);
        return duplikate.Count == 0 || await DuplikatDialog.ZeigenAsync(dialog, duplikate);
    }
}
