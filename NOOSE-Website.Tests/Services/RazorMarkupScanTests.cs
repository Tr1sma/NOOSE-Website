using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard over Razor markup traps the compiler accepts and the renderer does not.</summary>
public partial class RazorMarkupScanTests
{
    [Fact]
    public void NoRazorCommentSitsInsideAComponentsAttributeList()
    {
        // the defect this pins: a @* … *@ between two attributes of <StatTile> compiled cleanly and then threw
        // "does not have a property matching the name '@* … *@'" at render time, taking the whole settings tab
        // down behind the ErrorBoundary. Razor reads it as an attribute NAME, not as a comment.
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Root(), "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match tag in OpenTag().Matches(text))
            {
                if (tag.Groups["attrs"].Value.Contains("@*", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}: <{tag.Groups["name"].Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Ein Razor-Kommentar zwischen zwei Attributen wird als Parametername gebunden und wirft beim Rendern; "
            + "er gehört über das Tag: " + string.Join(", ", offenders.Order(StringComparer.Ordinal)));
    }

    /// <summary>An opening tag of a component (capitalised), with its attribute list captured.</summary>
    [GeneratedRegex(@"<(?<name>[A-Z][A-Za-z0-9]*)\b(?<attrs>(?:[^<>""]|""[^""]*"")*?)/?>", RegexOptions.Singleline)]
    private static partial Regex OpenTag();

    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website", "Components"));
}
