using System.Runtime.CompilerServices;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard that the score sparkline is actually rendered somewhere.</summary>
/// <remarks>
/// The defect this pins is not a crash but a silence: <c>ThreatSparkline</c> and
/// <c>IThreatTrendService.GetSparklinesAsync</c> were both built, tested and then referenced by no view at all -
/// for months, with nothing red. A component nobody renders is indistinguishable from a component that works.
/// <para>
/// Removing a curve from one of these views is a decision, not an accident: take the file out of this list and
/// say why in the commit.
/// </para>
/// </remarks>
public class ScoreTrendWiringTests
{
    /// <summary>The views that carry a score and therefore owe the reader its direction.</summary>
    private static readonly string[][] Wired =
    [
        ["Pages", "People", "PeopleList.razor"],
        ["Pages", "Factions", "FactionsList.razor"],
        ["Pages", "Watchlist", "MyBeobachteten.razor"],
        ["Common", "Shared", "HazardList.razor"],
    ];

    [Fact]
    public void EveryScoredListDrawsTheTrend()
    {
        var missing = Wired
            .Select(parts => Path.Combine([Root(), .. parts]))
            .Where(path => !File.ReadAllText(path).Contains("<ThreatSparkline", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(missing.Count == 0,
            "Diese Ansichten zeigen einen Bedrohungs-Score, aber keinen Verlauf: "
            + string.Join(", ", missing.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void EveryDrawnTrendCarriesItsCaption()
    {
        // the curve has no numbers on it; without the tooltip the reader sees a shape and no scale
        var mute = Wired
            .Select(parts => Path.Combine([Root(), .. parts]))
            .Where(path => !File.ReadAllText(path).Contains("ThreatTrendText.Hint", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(mute.Count == 0,
            "Diese Ansichten zeichnen den Verlauf ohne erklärenden Hinweis: "
            + string.Join(", ", mute.Order(StringComparer.Ordinal)));
    }

    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website", "Components"));
}
