using MudBlazor;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Pages.People.Shared;

/// <summary>Duplicate check before quick-create.</summary>
public static class DuplicateCheck
{
    /// <summary>True if user confirmed or name is empty.</summary>
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
