using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace NOOSE_Website.Components.Common.Shared;

/// <summary>Keeps list filters in the query string so back-navigation and shared links restore the view.</summary>
public static class QueryState
{
    /// <summary>Reads a query parameter, or null when absent or empty.</summary>
    public static string? Read(NavigationManager nav, string name) => Read(new Uri(nav.Uri).Query, name);

    /// <summary>Reads a query parameter out of a query string, with or without its leading "?".</summary>
    /// <remarks>
    /// The string form exists because <see cref="NavigationManager.Uri"/> lags: <see cref="WriteAsync"/> goes through
    /// <c>replaceState</c>, which Blazor never hears about. A caller holding the real address reads from that.
    /// </remarks>
    public static string? Read(string? query, string name)
    {
        var parsed = QueryHelpers.ParseQuery(query);
        if (parsed.TryGetValue(name, out var values) && values.Count > 0 && !string.IsNullOrWhiteSpace(values[0]))
        {
            return values[0];
        }
        return null;
    }

    /// <summary>Reads an enum query parameter, or null when absent or not a member.</summary>
    public static TEnum? ReadEnum<TEnum>(NavigationManager nav, string name) where TEnum : struct, Enum
        => ReadEnum<TEnum>(new Uri(nav.Uri).Query, name);

    /// <summary>Reads an enum query parameter out of a query string, or null when absent or not a member.</summary>
    public static TEnum? ReadEnum<TEnum>(string? query, string name) where TEnum : struct, Enum
        // TryParse alone takes any number: "?einstufung=99" came back as a value no switch knows
        => Enum.TryParse<TEnum>(Read(query, name), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;

    /// <summary>Reads a bool query parameter; absent or unparsable yields false.</summary>
    public static bool ReadFlag(NavigationManager nav, string name) => ReadFlag(new Uri(nav.Uri).Query, name);

    /// <summary>Reads a bool query parameter out of a query string; "1" and "true" both count.</summary>
    public static bool ReadFlag(string? query, string name)
        // "1" too: the one caller before this wrote "1" and read it back strictly, so its box never survived a reload
        => Read(query, name) is { } raw
           && (raw == "1" || (bool.TryParse(raw, out var parsed) && parsed));

    /// <summary>Whether any of the pairs carries a value; an all-empty set is the list's default view.</summary>
    public static bool AnySet(params (string Name, string? Value)[] values)
        => values.Any(v => !string.IsNullOrWhiteSpace(v.Value));

    /// <summary>A relative address carrying just the set pairs, in the order given.</summary>
    /// <remarks>
    /// Built from the page's own fields rather than read off the address bar, which still shows the state before
    /// the last filter change. Empty values drop out exactly as they do in <see cref="WriteAsync"/>.
    /// </remarks>
    public static string BuildRoute(string baseRoute, params (string Name, string? Value)[] values)
    {
        var set = values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToList();
        return set.Count == 0
            ? baseRoute
            : baseRoute + "?" + string.Join("&",
                set.Select(v => Uri.EscapeDataString(v.Name) + "=" + Uri.EscapeDataString(v.Value!)));
    }

    /// <summary>Writes the parameters without a Blazor navigation; null or empty values are removed.</summary>
    public static async Task WriteAsync(IJSRuntime js, NavigationManager nav, params (string Name, string? Value)[] values)
    {
        var parameters = values.ToDictionary(
            v => v.Name,
            v => string.IsNullOrWhiteSpace(v.Value) ? null : (object?)v.Value);

        var url = nav.GetUriWithQueryParameters(parameters);
        try { await js.InvokeVoidAsync("nooseReplaceState", url); }
        catch (JSDisconnectedException) { /* ignore */ }
    }
}
