using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The public wanted snapshot has one save path and one cache key; a file scan keeps it that way.</summary>
/// <remarks>
/// Before Phase 5 the invalidation was ten separate <c>cache.Remove</c> calls next to ten separate saves. Every new
/// write path was one more chance to forget one, and the archive would have doubled them again. The shape below is
/// the guarantee — not the care taken during review.
/// </remarks>
public partial class PublicWantedCacheDisciplineTests
{
    private const string CacheKeyLiteral = "\"OeffentlicheFahndungen\"";

    private static string ServiceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "NOOSE-Website", "Services", "Public"));

    private static string ServiceFile()
    {
        var file = Path.Combine(ServiceRoot(), "PublicWantedService.cs");
        Assert.True(File.Exists(file), $"Dienst nicht gefunden: {file}");
        return file;
    }

    [Fact]
    public void TheWantedService_SavesThroughExactlyOneChokePoint()
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
        // a second file holding the key could invalidate half the snapshot, or forget to; the assignment writes in
        // Phase 5 therefore live on PublicWantedService rather than on the warning value-list service
        var offenders = Directory.EnumerateFiles(ServiceRoot(), "*.cs")
            .Where(f => Path.GetFileName(f) != "PublicWantedService.cs")
            .Where(f => File.ReadAllText(f).Contains(CacheKeyLiteral, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Nur PublicWantedService kennt den Snapshot-Schlüssel: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"\bSaveChangesAsync\(")]
    private static partial Regex SaveChanges();

    [GeneratedRegex(@"cache\.Remove\(")]
    private static partial Regex CacheRemove();

    [GeneratedRegex(@"cache\.Set\(")]
    private static partial Regex CacheSet();
}
