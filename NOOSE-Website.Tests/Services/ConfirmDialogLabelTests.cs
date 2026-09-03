using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard over the shared confirmation dialog's button label.</summary>
/// <remarks>
/// The reported defect: "Seite veröffentlichen?" offered only "Abbrechen" and a red "Löschen". The destructive
/// default is correct for the 62 deletions and wrong for everything else, and nothing but a scan can tell the two
/// apart — the compiler is happy either way.
/// </remarks>
public class ConfirmDialogLabelTests
{
    /// <summary>A title that names a deletion may lean on the default; every other verb must be spelled out.</summary>
    private static readonly string[] DestructiveWords = ["lösch", "entfern", "endgültig"];

    /// <summary>Positional index of confirmText per helper; anything at or past it counts as explicit.</summary>
    private static readonly Dictionary<string, int> LabelArgument = new(StringComparer.Ordinal)
    {
        ["ConfirmDialog"] = 3,
        ["ConfirmTypedDialog"] = 4,
    };

    [Fact]
    public void EveryNonDestructiveConfirmationNamesItsOwnVerb()
    {
        var offenders = Calls()
            .Where(c => !DestructiveWords.Any(w => c.Title.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Where(c => c.Arguments <= LabelArgument[c.Helper])
            .Select(c => $"{c.File}:{c.Line} {c.Title}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Diese Bestätigung erbt das rote \"Löschen\", obwohl sie nichts löscht — eigenes confirmText übergeben: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void TheScanActuallyFindsCalls()
        => Assert.True(Calls().Count > 50, "Der Scan findet keine Aufrufe mehr; die Regex ist veraltet.");

    private sealed record Call(string File, int Line, string Helper, string Title, int Arguments);

    private static List<Call> Calls()
    {
        var calls = new List<Call>();
        foreach (var file in Directory.EnumerateFiles(Root(), "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"(ConfirmTypedDialog|ConfirmDialog)\.ShowAsync\("))
            {
                var body = Body(text, match.Index + match.Length);
                if (body is null)
                {
                    continue;
                }
                var title = Regex.Match(body, "\"([^\"]*)\"");
                calls.Add(new Call(
                    Path.GetFileName(file),
                    text.Take(match.Index).Count(c => c == '\n') + 1,
                    match.Groups[1].Value,
                    title.Success ? title.Groups[1].Value : string.Empty,
                    TopLevelArguments(body)));
            }
        }
        return calls;
    }

    /// <summary>Text between the opening parenthesis and its match, or null when the call is unbalanced.</summary>
    private static string? Body(string text, int start)
    {
        var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            depth += text[i] switch { '(' => 1, ')' => -1, _ => 0 };
            if (depth == 0)
            {
                return text[start..i];
            }
        }
        return null;
    }

    private static int TopLevelArguments(string body)
    {
        var count = 1;
        var depth = 0;
        var inString = false;
        foreach (var c in body)
        {
            if (c == '"')
            {
                inString = !inString;
                continue;
            }
            if (inString)
            {
                continue;
            }
            depth += c switch { '(' or '[' or '{' => 1, ')' or ']' or '}' => -1, _ => 0 };
            if (c == ',' && depth == 0)
            {
                count++;
            }
        }
        return count;
    }

    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website", "Components"));
}
