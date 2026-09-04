using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The FAQ snapshot has one save path and one cache key; a file scan keeps it that way.</summary>
/// <remarks>
/// The sibling of <see cref="PublicWarningCacheDisciplineTests"/>, with its own key for the same reason: different
/// tables, invalidated by different writes. Sharing a key would let one write drop a snapshot it never touched.
/// <para>
/// Two tables behind one snapshot, so the last fact matters twice over: a rubric write that forgot the choke point
/// would leave the questions under it showing a stale heading.
/// </para>
/// </remarks>
public partial class PublicFaqCacheDisciplineTests
{
    private const string CacheKeyLiteral = "\"OeffentlichesFaq\"";
    private const string ServiceName = "PublicFaqService.cs";

    private static readonly string[] Tables = ["db.OeffentlicheFaqRubriken", "db.OeffentlicheFaqEintraege"];

    private static string ServiceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "NOOSE-Website", "Services", "Public"));

    private static string ServiceFile()
    {
        var file = Path.Combine(ServiceRoot(), ServiceName);
        Assert.True(File.Exists(file), $"Dienst nicht gefunden: {file}");
        return file;
    }

    [Fact]
    public void TheFaqService_SavesThroughExactlyOneChokePoint()
    {
        var text = File.ReadAllText(ServiceFile());
        Assert.Single(SaveChanges().Matches(text));
    }

    [Fact]
    public void TheSnapshot_IsDroppedOnlyInThatChokePoint()
    {
        var text = File.ReadAllText(ServiceFile());
        Assert.Single(CacheRemove().Matches(text));
        Assert.Single(CacheSet().Matches(text));
    }

    [Fact]
    public void NoOtherPublicServiceKnowsTheCacheKey()
    {
        var offenders = Directory.EnumerateFiles(ServiceRoot(), "*.cs")
            .Where(f => Path.GetFileName(f) != ServiceName)
            .Where(f => File.ReadAllText(f).Contains(CacheKeyLiteral, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Nur PublicFaqService kennt den Snapshot-Schlüssel: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryWriterOfTheFaqTables_IsThatOneService()
    {
        var offenders = Directory.EnumerateFiles(
                Path.GetFullPath(Path.Combine(ServiceRoot(), "..", "..")), "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != ServiceName)
            .Where(f => !f.Contains(Path.Combine("Data", "Migrations"), StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f) is var t
                && Tables.Any(table => t.Contains(table, StringComparison.Ordinal))
                && SaveChanges().IsMatch(t))
            .Select(Path.GetFileName)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Wer eine FAQ-Tabelle schreibt, muss den Snapshot verwerfen: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"\bSaveChangesAsync\(")]
    private static partial Regex SaveChanges();

    [GeneratedRegex(@"cache\.Remove\(")]
    private static partial Regex CacheRemove();

    [GeneratedRegex(@"cache\.Set\(")]
    private static partial Regex CacheSet();
}
