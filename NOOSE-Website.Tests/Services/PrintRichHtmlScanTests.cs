using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard: rich text on a print page must render plain.</summary>
/// <remarks>
/// <c>RichHtml</c> decides on <c>Plain</c> whether to resolve mentions as links and whether to mark glossary
/// terms. Every print page passes it today, which is the only reason a bubble cannot reach paper - and route
/// sniffing is no substitute, because not every print page lives under a /druck address
/// (<c>/lageberichte/{Id}</c> uses the print layout too). A new print page that forgets it turns this red
/// instead of shipping a dotted underline into a PDF that leaves the building.
/// </remarks>
public sealed class PrintRichHtmlScanTests
{
    private static string SourceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website"));

    [Fact]
    public void EveryRichHtmlOnAPrintPageRendersPlain()
    {
        var root = SourceRoot();
        Assert.True(Directory.Exists(root), $"Quellordner nicht gefunden: {root}");

        var printPages = Directory
            .EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            .Where(f => Regex.IsMatch(f.Text, @"^\s*@layout\s+PrintLayout\b", RegexOptions.Multiline))
            .ToList();

        // if this ever hits zero the guard has quietly stopped guarding anything
        Assert.NotEmpty(printPages);

        var offenders = printPages
            .SelectMany(page => Regex
                .Matches(page.Text, @"<RichHtml\b[^>]*?/?>", RegexOptions.Singleline)
                .Where(m => !m.Value.Contains("Plain=\"true\"", StringComparison.Ordinal))
                .Select(m => $"{Path.GetRelativePath(root, page.Path)}: {Collapse(m.Value)}"))
            .Order()
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string Collapse(string tag) => Regex.Replace(tag, @"\s+", " ").Trim();
}
