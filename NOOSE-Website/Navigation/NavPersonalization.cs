using NOOSE_Website.Models.Navigation;

namespace NOOSE_Website.Navigation;

/// <summary>Applies a user's hidden/order preferences to catalog entries.</summary>
public static class NavPersonalization
{
    /// <summary>Drop hidden keys, then apply custom order.</summary>
    public static IReadOnlyList<NavEntry> Apply(IEnumerable<NavEntry> entries, NavPreferences prefs)
        => Ordered(entries.Where(e => !prefs.HiddenKeys.Contains(e.Key)), prefs);

    /// <summary>Apply custom order only (keeps hidden entries; stable for unordered keys).</summary>
    public static IReadOnlyList<NavEntry> Ordered(IEnumerable<NavEntry> entries, NavPreferences prefs)
        => entries.OrderBy(e => OrderIndex(prefs.Order, e.Key)).ToList();

    private static int OrderIndex(List<string> order, string key)
    {
        var i = order.IndexOf(key);
        return i < 0 ? int.MaxValue : i;
    }
}
