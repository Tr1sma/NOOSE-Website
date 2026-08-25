using MudBlazor;

namespace NOOSE_Website.Services.Public;

/// <summary>One selectable chip colour: the stored name, its German label and the MudBlazor value.</summary>
public sealed record WarnhinweisColourChoice(string Name, string Label, Color Colour);

/// <summary>The colours a warning chip may use, as an allowlist.</summary>
/// <remarks>
/// Never <c>Enum.Parse</c>. A value that reached the column any other way — a typo, an old MudBlazor name — would
/// throw while rendering an [AllowAnonymous] page, which is an HTTP 500 on a URL anyone can call. And the list holds
/// none of the colours that are invisible on the public background (Inherit, Transparent, Surface, Dark): a warning
/// nobody can see is worse than no warning.
/// </remarks>
public static class WarnhinweisColours
{
    public static readonly IReadOnlyList<WarnhinweisColourChoice> All =
    [
        new("Error", "Rot", Color.Error),
        new("Warning", "Orange", Color.Warning),
        new("Info", "Blau", Color.Info),
        new("Success", "Grün", Color.Success),
        new("Secondary", "Violett", Color.Secondary),
        new("Default", "Grau", Color.Default),
    ];

    /// <summary>The MudBlazor colour for a stored name; anything unknown or empty falls back to grey.</summary>
    public static Color Resolve(string? name)
        => All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal))?.Colour ?? Color.Default;

    public static bool IsKnown(string? name)
        => All.Any(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    /// <summary>The stored name if it is on the list, otherwise null — the write path never keeps a stray value.</summary>
    public static string? Sanitise(string? name) => IsKnown(name) ? name : null;
}
