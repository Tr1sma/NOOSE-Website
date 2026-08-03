using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace NOOSE_Website.Components.Common.Shared;

/// <summary>Keeps list filters in the query string so back-navigation and shared links restore the view.</summary>
public static class QueryState
{
    /// <summary>Reads a query parameter, or null when absent or empty.</summary>
    public static string? Read(NavigationManager nav, string name)
    {
        var query = QueryHelpers.ParseQuery(new Uri(nav.Uri).Query);
        if (query.TryGetValue(name, out var values) && values.Count > 0 && !string.IsNullOrWhiteSpace(values[0]))
        {
            return values[0];
        }
        return null;
    }

    /// <summary>Reads an enum query parameter, or null when absent or not a member.</summary>
    public static TEnum? ReadEnum<TEnum>(NavigationManager nav, string name) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(Read(nav, name), ignoreCase: true, out var parsed) ? parsed : null;

    /// <summary>Reads a bool query parameter; absent or unparsable yields false.</summary>
    public static bool ReadFlag(NavigationManager nav, string name)
        => bool.TryParse(Read(nav, name), out var parsed) && parsed;

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
