using System.Text.RegularExpressions;

namespace NOOSE_Website.Services;

/// <summary>When a typed slash asks for a text snippet, what inserting one does, and how one reads in a list.</summary>
/// <remarks>
/// The same shape as the <c>@</c>-mention trigger in <c>MentionInput</c>: anchored at the end of the text, because
/// <c>MudTextField</c> hands out no caret and the whole insertion path there works on the tail. The one difference is
/// the word boundary in front - an <c>@</c> in the middle of a word is rare, a slash is not, and without it
/// "Vinewood/Ost" would open the list while somebody types a street.
/// </remarks>
public static partial class SnippetTrigger
{
    [GeneratedRegex(@"(?:^|\s)/([^\s/]*)$")]
    private static partial Regex TriggerRegex();

    /// <summary>What the agent typed after the slash, or null when no slash is asking.</summary>
    /// <remarks>An empty string means the slash stands alone; callers wait for a letter, as the mention picker does.</remarks>
    public static string? Query(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        var match = TriggerRegex().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>One line of a snippet, so a picker row shows what it is about.</summary>
    /// <remarks>Both surfaces show the same row; the rule lived twice and would have drifted.</remarks>
    public static string Preview(string? text)
    {
        var flat = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return flat.Length <= 70 ? flat : flat[..70] + "…";
    }

    /// <summary>Replaces the asking slash and its query with the snippet; appends when nothing is asking.</summary>
    /// <param name="max">The field's own limit. A snippet longer than the field is cut, not refused.</param>
    /// <remarks>
    /// The limit is not decoration: <c>MudTextField.MaxLength</c> only stops typing, so a snippet written
    /// straight into the bound string walks past it - and past the column behind it. Cutting is what the
    /// field would have done to the same characters typed by hand.
    /// </remarks>
    public static string Insert(string? text, string? snippet, int max = int.MaxValue)
    {
        var current = text ?? string.Empty;
        var body = snippet ?? string.Empty;
        var match = TriggerRegex().Match(current);
        // the match may start on the space before the slash; that space is the agent's, not ours
        var cut = match.Success ? match.Index + (current[match.Index] == '/' ? 0 : 1) : current.Length;
        var result = string.Concat(current.AsSpan(0, cut), body);
        return max > 0 && result.Length > max ? result[..max] : result;
    }
}
