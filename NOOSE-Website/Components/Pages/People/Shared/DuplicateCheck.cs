using MudBlazor;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Pages.People.Shared;

/// <summary>
/// Gemeinsame Dubletten-Prüfung für die Schnellanlage-Pfade (Dok für neue Person, Mitglied „neue Person
/// anlegen"). Zeigt bei gleichnamigen Akten den Warn-Dialog – damit nicht nur das reguläre
/// „Neue Person"-Formular vor Dubletten warnt.
/// </summary>
public static class DuplicateCheck
{
    /// <summary>
    /// Liefert true, wenn die neue Akte angelegt werden darf: kein gleichnamiger Treffer ODER der Nutzer
    /// hat „Trotzdem anlegen" bestätigt. Bei leerem Namen wird nicht geprüft (true).
    /// </summary>
    public static async Task<bool> MayNewRecordCreateAsync(
        IDialogService dialog, IPersonService personService, string? newName, bool isLeadership, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return true;
        }
        var duplicates = await personService.FindDuplicatesAsync(newName.Trim(), Array.Empty<string>(), isLeadership, cancellationToken);
        return duplicates.Count == 0 || await DuplicateDialog.ShowAsync(dialog, duplicates);
    }
}
