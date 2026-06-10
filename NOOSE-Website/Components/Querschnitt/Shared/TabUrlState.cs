using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace NOOSE_Website.Components.Querschnitt.Shared;

/// <summary>
/// Hält den aktiven Tab einer Detailseite in der Adresse (?tab=…) fest, damit er nach dem
/// Speichern, nach F5/Neuladen, über den Zurück-Button und beim Teilen eines Links erhalten bleibt.
/// Die Slugs übergibt jede Seite in Panel-Reihenfolge; ein fehlender oder unbekannter Wert
/// führt zum ersten Tab.
/// </summary>
public static class TabUrlState
{
    private const string ParameterName = "tab";

    /// <summary>Liest den aktiven Panel-Index aus der aktuellen URL (0, falls nicht vorhanden oder unbekannt).</summary>
    public static int Read(NavigationManager nav, IReadOnlyList<string> slugs)
    {
        var query = QueryHelpers.ParseQuery(new Uri(nav.Uri).Query);
        if (query.TryGetValue(ParameterName, out var werte) && werte.Count > 0)
        {
            var index = IndexVon(slugs, werte[0]);
            if (index >= 0)
            {
                return index;
            }
        }
        return 0;
    }

    /// <summary>Schreibt den Slug des aktiven Panels in die URL und ersetzt dabei den Verlaufseintrag (kein History-Spam).</summary>
    public static void Write(NavigationManager nav, IReadOnlyList<string> slugs, int index)
    {
        if (index < 0 || index >= slugs.Count)
        {
            return;
        }
        var ziel = nav.GetUriWithQueryParameter(ParameterName, slugs[index]);
        nav.NavigateTo(ziel, forceLoad: false, replace: true);
    }

    private static int IndexVon(IReadOnlyList<string> slugs, string? slug)
    {
        for (var i = 0; i < slugs.Count; i++)
        {
            if (string.Equals(slugs[i], slug, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}
