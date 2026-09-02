using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The press snapshot has one save path and one cache key; a file scan keeps it that way.</summary>
/// <remarks>
/// The sibling of <see cref="PublicWantedCacheDisciplineTests"/> and
/// <see cref="PublicFactionProfileCacheDisciplineTests"/>, with its own key for the same reason: a different table,
/// invalidated by different writes. Sharing a key would let one write drop a snapshot it never touched.
/// </remarks>
public partial class PressCacheDisciplineTests
{
    private const string CacheKeyLiteral = "\"Pressemitteilungen\"";
    private const string ServiceName = "PressReleaseService.cs";

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
    public void ThePressService_SavesThroughExactlyOneChokePoint()
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
            "Nur PressReleaseService kennt den Snapshot-Schlüssel: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryWriterOfThePressTable_IsThatOneService()
    {
        // the capture hook leaves a draft behind by calling the service, not by touching the table: a second writer
        // would leave the snapshot standing
        var offenders = Directory.EnumerateFiles(
                Path.GetFullPath(Path.Combine(ServiceRoot(), "..", "..")), "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != ServiceName)
            .Where(f => !f.Contains(Path.Combine("Data", "Migrations"), StringComparison.Ordinal))
            // AgentManagementService only clears the agent pointer on these rows when an account is hard-
            // deleted. No outward record carries a publisher, handler or donor, so no cached snapshot can
            // go stale from it - and its writes are ExecuteUpdate, matched here only because the file
            // contains a SaveChangesAsync elsewhere.
            .Where(f => Path.GetFileName(f) != "AgentManagementService.cs")
            .Where(f => File.ReadAllText(f) is var t
                && t.Contains("db.Pressemitteilungen", StringComparison.Ordinal)
                && SaveChanges().IsMatch(t))
            .Select(Path.GetFileName)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Wer die Pressetabelle schreibt, muss den Snapshot verwerfen: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"\bSaveChangesAsync\(")]
    private static partial Regex SaveChanges();

    [GeneratedRegex(@"cache\.Remove\(")]
    private static partial Regex CacheRemove();

    [GeneratedRegex(@"cache\.Set\(")]
    private static partial Regex CacheSet();
}
