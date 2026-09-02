using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>
/// Two query shapes that were each wrong in three public services at once, pinned so the next one is caught by the
/// build rather than by a visitor.
/// </summary>
public partial class PublicReadShapeTests
{
    private static string ServiceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "NOOSE-Website", "Services", "Public"));

    private static (string File, string Text)[] Services()
    {
        var root = ServiceRoot();
        Assert.True(Directory.Exists(root), $"Dienstordner nicht gefunden: {root}");
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => (File: Path.GetFileName(f), Text: WithoutComments(File.ReadAllText(f))))
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .ToArray();
        // a wrong path would otherwise leave both facts below green forever
        Assert.NotEmpty(files);
        return files;
    }

    /// <summary>Prose that explains why a file avoids a shape contains the very shape.</summary>
    private static string WithoutComments(string text)
    {
        text = BlockComment().Replace(text, " ");
        return LineComment().Replace(text, " ");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//.*")]
    private static partial Regex LineComment();

    /// <summary>
    /// A lookup dictionary must not be built from a capped list: the cap is a page-weight decision about a hub,
    /// while the dictionary answers a permanent public address.
    /// </summary>
    /// <remarks>
    /// This was wrong for the press releases and the monthly reports at the same time: both derived Cards AND
    /// ByCaseNumber/ByPeriod from one <c>.Take(HubLimit)</c> list, so an older release answered "not published" at
    /// the very address its own non-recallable Discord post pointed at. The rule: whoever builds such a dictionary
    /// resolves a single row instead, which is what the fix did in both.
    /// </remarks>
    [Fact]
    public void NoAddressLookupIsBuiltFromACappedList()
    {
        var offenders = Services()
            .Where(f => CappedTake().IsMatch(f.Text))
            .Where(f => AddressDictionary().IsMatch(f.Text))
            // the fix shape: the address is answered by its own single-row read
            .Where(f => !f.Text.Contains("FirstOrDefaultAsync", StringComparison.Ordinal))
            .Select(f => f.File)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Ein Adress-Wörterbuch darf nicht aus einer gedeckelten Liste stammen; die Adresse braucht einen "
            + "eigenen Einzelzeilen-Lesepfad: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"\.Take\((?:\w+\.)?(?:HubLimit|ArchiveLimit)\)")]
    private static partial Regex CappedTake();

    [GeneratedRegex(@"(?:ByCaseNumber|ByPeriod)\s*[:=]")]
    private static partial Regex AddressDictionary();

    /// <summary>
    /// Two columns compared inside an EF projection are compared by the SERVER, under a collation nobody configured
    /// - and the default is case AND accent insensitive.
    /// </summary>
    /// <remarks>
    /// That is how "Entwurf abweichend" answered "nothing to publish" after a capital letter or an umlaut was
    /// corrected, in the press, page and report panels alike. A length comparison alongside the equality is the
    /// cheap half of the fix (the bounded strings are compared ordinally in memory), so this fact demands that
    /// second term wherever the shape appears at all.
    /// </remarks>
    [Fact]
    public void EveryColumnComparisonIsBackedByALengthComparison()
    {
        var offenders = Services()
            .Where(f => ColumnComparison().IsMatch(f.Text))
            .Where(f => !LengthComparison().IsMatch(f.Text))
            .Select(f => f.File)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Ein Spaltenvergleich in einer EF-Projektion läuft unter der Server-Kollation (case- und "
            + "akzent-insensitiv) und braucht einen Längenvergleich daneben: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"\?\?\s*string\.Empty\)\s*!=\s*\(")]
    private static partial Regex ColumnComparison();

    [GeneratedRegex(@"\.Length\s*!=\s*\(")]
    private static partial Regex LengthComparison();
}
