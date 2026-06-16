using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace NOOSE_Website.Components.Common.Shared;

/// <summary>Syncs active tab to URL query param.</summary>
public static class TabUrlState
{
    private const string ParameterName = "tab";

    /// <summary>Reads active tab index.</summary>
    public static int Read(NavigationManager nav, IReadOnlyList<string> slugs)
    {
        var query = QueryHelpers.ParseQuery(new Uri(nav.Uri).Query);
        if (query.TryGetValue(ParameterName, out var values) && values.Count > 0)
        {
            var index = IndexBy(slugs, values[0]);
            if (index >= 0)
            {
                return index;
            }
        }
        return 0;
    }

    /// <summary>Writes active tab slug.</summary>
    public static void Write(NavigationManager nav, IReadOnlyList<string> slugs, int index)
    {
        if (index < 0 || index >= slugs.Count)
        {
            return;
        }
        var target = nav.GetUriWithQueryParameter(ParameterName, slugs[index]);
        nav.NavigateTo(target, forceLoad: false, replace: true);
    }

    private static int IndexBy(IReadOnlyList<string> slugs, string? slug)
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
