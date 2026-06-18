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
}
