using NOOSE_Website.Models.Navigation;

namespace NOOSE_Website.Services;

/// <summary>What saving a list view came to.</summary>
public enum SavedViewOutcome
{
    /// <summary>A new view was added at the end.</summary>
    Added,

    /// <summary>A view of the same name existed and now points at the new route.</summary>
    Replaced,

    /// <summary>The cap is reached and the name is new; nothing changed.</summary>
    Full,

    /// <summary>No name, or a route that is not a local address; nothing changed.</summary>
    Invalid,
}

/// <summary>Rules for an agent's saved list views: what counts as the same one, how many, and which list owns it.</summary>
/// <remarks>
/// Static and pure, like <see cref="Onboarding"/>: the views live in the preferences blob, so every rule here is
/// applied to a plain list and needs no database to be tested.
/// </remarks>
public static class SavedViewRules
{
    /// <summary>Most views one agent keeps, across all lists.</summary>
    /// <remarks>The drawer lists them all, and so does the palette before anything is typed; past this they stop
    /// being a shortcut.</remarks>
    public const int Cap = 20;

    /// <summary>Longest name; the drawer row is one line.</summary>
    public const int MaxLabelLength = 60;

    /// <summary>Longest route; a free-text search term is the only part that can grow.</summary>
    public const int MaxRouteLength = 2000;

    /// <summary>Saves a view under its name. The same name on the same list, in any case, overwrites.</summary>
    /// <remarks>
    /// Names are unique per list, not overall: "Rot" on the people list and "Rot" on the factions list are two
    /// views, and saving the second must not quietly carry the first one off to another list. The palette tells
    /// them apart by the list name it shows beside them. Overwriting keeps the id and the place in the list, so
    /// re-saving a corrected filter does not move it. The cap only stops a new view - replacing one needs no slot.
    /// </remarks>
    public static SavedViewOutcome Add(List<SavedView> views, string? label, string? route, string? icon)
    {
        var name = Normalise(label);
        if (name.Length == 0 || !IsLocalRoute(route))
        {
            return SavedViewOutcome.Invalid;
        }

        var path = PathOf(route);
        var index = views.FindIndex(v => SameName(v.Label, name) && SamePath(v.Route, path));
        if (index >= 0)
        {
            views[index] = views[index] with { Label = name, Route = route!, Icon = icon ?? string.Empty };
            return SavedViewOutcome.Replaced;
        }
        if (views.Count >= Cap)
        {
            return SavedViewOutcome.Full;
        }
        views.Add(new SavedView(Guid.NewGuid().ToString("N"), name, route!, icon ?? string.Empty));
        return SavedViewOutcome.Added;
    }

    /// <summary>Removes a view by its id; false when there was none.</summary>
    public static bool Remove(List<SavedView> views, string? id)
        => !string.IsNullOrEmpty(id) && views.RemoveAll(v => v.Id == id) > 0;

    /// <summary>The views that belong to one list, in their saved order.</summary>
    /// <remarks>
    /// The path must match exactly. A prefix match would hand the views of <c>/personengruppen</c> to
    /// <c>/personen</c>, and a record page such as <c>/personen/{id}</c> is not the list either.
    /// </remarks>
    public static IReadOnlyList<SavedView> ForRoute(IEnumerable<SavedView> views, string? baseRoute)
    {
        var path = PathOf(baseRoute);
        return views.Where(v => SamePath(v.Route, path)).ToList();
    }

    /// <summary>Whether a name is already taken among the given views, ignoring case and surrounding space.</summary>
    /// <remarks>Pass one list's views (<see cref="ForRoute"/>): that is the scope in which a name overwrites.</remarks>
    public static bool IsTaken(IEnumerable<SavedView> views, string? label)
    {
        var name = Normalise(label);
        return name.Length > 0 && views.Any(v => SameName(v.Label, name));
    }

    /// <summary>The path of a relative route, without query or fragment, with one leading slash.</summary>
    /// <remarks>Accepts the base-relative form <c>NavigationManager</c> hands out, which has no leading slash.</remarks>
    public static string PathOf(string? route)
    {
        var path = (route ?? string.Empty).Split('?', 2)[0].Split('#', 2)[0].Trim().Trim('/');
        return "/" + path;
    }

    /// <summary>A route the app can navigate to without leaving itself.</summary>
    /// <remarks>
    /// The route is built from the page's own base route, so this never trips in practice. It stays because the
    /// drawer and the palette put it straight into an <c>href</c>: "//host" or a backslash would leave the site.
    /// </remarks>
    public static bool IsLocalRoute(string? route)
        => !string.IsNullOrWhiteSpace(route)
           && route.Length <= MaxRouteLength
           && route[0] == '/'
           && !route.StartsWith("//", StringComparison.Ordinal)
           && !route.Contains('\\')
           && !route.Any(char.IsControl);

    /// <summary>The name as it is stored: trimmed, inner runs of space collapsed, cut to the limit.</summary>
    public static string Normalise(string? label)
    {
        var name = string.Join(' ', (label ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return name.Length <= MaxLabelLength ? name : name[..MaxLabelLength].TrimEnd();
    }

    private static bool SameName(string? stored, string name)
        => string.Equals(Normalise(stored), name, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string? route, string path)
        => string.Equals(PathOf(route), path, StringComparison.OrdinalIgnoreCase);
}
