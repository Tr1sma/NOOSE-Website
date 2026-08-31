using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The released-report snapshot has one save path and one cache key; a file scan keeps it that way.</summary>
/// <remarks>
/// The sibling of <see cref="PressCacheDisciplineTests"/> and <see cref="PublicWarningCacheDisciplineTests"/>, with
/// its own key for the same reason: a different table, invalidated by different writes.
/// <para>
/// Unlike the law page there is no second writer to allow for. The service reads <c>db.SituationReports</c> to
/// resolve an anchor, but it never writes that table — the archive belongs to <c>SituationReportService</c>, and the
/// outward text carries no field of the snapshot, so a change there cannot make a published report wrong.
/// </para>
/// </remarks>
public partial class PublicReportCacheDisciplineTests
{
    private const string CacheKeyLiteral = "\"OeffentlicheLageberichte\"";
    private const string ServiceName = "PublicReportService.cs";

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
    public void ThePublicReportService_SavesThroughExactlyOneChokePoint()
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
            "Nur PublicReportService kennt den Snapshot-Schlüssel: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryWriterOfTheReportTable_IsThatOneService()
    {
        var offenders = Directory.EnumerateFiles(
                Path.GetFullPath(Path.Combine(ServiceRoot(), "..", "..")), "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != ServiceName)
            .Where(f => !f.Contains(Path.Combine("Data", "Migrations"), StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f) is var t
                && t.Contains("db.OeffentlicheLageberichte", StringComparison.Ordinal)
                && SaveChanges().IsMatch(t))
            .Select(Path.GetFileName)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Wer die Lageberichts-Tabelle schreibt, muss den Snapshot verwerfen: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"\bSaveChangesAsync\(")]
    private static partial Regex SaveChanges();

    [GeneratedRegex(@"cache\.Remove\(")]
    private static partial Regex CacheRemove();

    [GeneratedRegex(@"cache\.Set\(")]
    private static partial Regex CacheSet();
}
