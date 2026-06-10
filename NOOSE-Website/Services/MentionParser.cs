using System.Text.RegularExpressions;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Parst und erzeugt @-Verlinkungs-Tokens der Form <c>@{Typ:Id}</c>. Die Id ist stets eine 36-stellige GUID
/// (alle Entitäten – inkl. Agent als IdentityUser – nutzen <c>Guid.NewGuid().ToString()</c>), daher kollidiert
/// das Token-Muster nicht mit gewöhnlichem Text. Rein textuell, UI-frei.
/// </summary>
public static partial class MentionParser
{
    [GeneratedRegex(@"@\{(?<typ>\w+):(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}")]
    private static partial Regex TokenRegex();

    /// <summary>Findet alle Mention-Tokens im Text (mit Position).</summary>
    public static IReadOnlyList<MentionToken> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<MentionToken>();
        }
        var liste = new List<MentionToken>();
        foreach (Match m in TokenRegex().Matches(text))
        {
            liste.Add(new MentionToken(m.Groups["typ"].Value, m.Groups["id"].Value, m.Index, m.Length));
        }
        return liste;
    }

    /// <summary>Bildet das Speicher-Token für einen Verweis: <c>@{Typ:Id}</c>.</summary>
    public static string Token(string typ, string id) => $"@{{{typ}:{id}}}";
}
