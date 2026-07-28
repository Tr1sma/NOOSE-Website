using System.Text.RegularExpressions;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Parses and builds @-mention tokens of the form <c>@{Type:Id}</c> where Id is a 36-char GUID.</summary>
public static partial class MentionParser
{
    [GeneratedRegex(@"@\{(?<typ>\w+):(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}")]
    private static partial Regex TokenRegex();

    /// <summary>Finds all mention tokens in the text, with position.</summary>
    public static IReadOnlyList<MentionToken> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<MentionToken>();
        }
        var list = new List<MentionToken>();
        foreach (Match m in TokenRegex().Matches(text))
        {
            list.Add(new MentionToken(m.Groups["typ"].Value, m.Groups["id"].Value, m.Index, m.Length));
        }
        return list;
    }

    /// <summary>Builds the storage token for a reference: <c>@{Type:Id}</c>.</summary>
    public static string Token(string type, string id) => $"@{{{type}:{id}}}";

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex GapRegex();

    /// <summary>Drops all tokens for contexts that never resolve them (search snippets, fuzzy matching, exports).</summary>
    public static string Strip(string? text)
    {
        // no '@' means no token; skips the regex on the overwhelming majority of texts
        if (string.IsNullOrEmpty(text) || !text.Contains('@'))
        {
            return text ?? string.Empty;
        }
        var bare = TokenRegex().Replace(text, " ");
        return GapRegex().Replace(bare, " ").Trim();
    }
}
