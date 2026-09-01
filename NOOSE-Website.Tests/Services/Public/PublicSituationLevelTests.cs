using MudBlazor;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The published situation level: a closed ladder, read back through an allowlist.</summary>
/// <remarks>
/// The stored value comes out of a hand-editable key/value row and is rendered on an [AllowAnonymous] page, so the
/// parser has to answer "unknown" rather than throw — the same failure class as an unparsable query value.
/// </remarks>
public class PublicSituationLevelTests
{
    private static readonly PublicSituationLevel[] Values =
        Enum.GetValues<PublicSituationLevel>();

    [Fact]
    public void AllListsEveryLevel()
        => Assert.Equal(Values.Order().ToArray(), PublicSituationLevelDisplay.All.Order().ToArray());

    [Fact]
    public void EveryLevelHasALabelAndAHint()
    {
        var offenders = Values
            .Where(l => PublicSituationLevelDisplay.Name(l) is "—" or ""
                || PublicSituationLevelDisplay.Hint(l).Length == 0)
            .Select(l => l.ToString())
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Jede Stufe hat Beschriftung und Kurztext: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryLevelHasItsOwnVisibleColour()
    {
        // a level nobody can see is worse than no level at all; the same allowlist argument as the warning chips
        Color[] invisible = [Color.Inherit, Color.Transparent, Color.Surface, Color.Dark, Color.Default];
        var colours = Values.Select(PublicSituationLevelDisplay.Colour).ToArray();

        var hidden = Values.Where(l => invisible.Contains(PublicSituationLevelDisplay.Colour(l)))
            .Select(l => l.ToString())
            .Order()
            .ToArray();
        Assert.True(hidden.Length == 0, "Unsichtbare Stufenfarbe: " + string.Join(", ", hidden));
        Assert.Equal(colours.Length, colours.Distinct().Count());
    }

    [Fact]
    public void ParseRoundTripsEveryStoredKey()
    {
        foreach (var level in Values)
        {
            Assert.Equal(level, PublicSituationLevelDisplay.Parse(PublicSituationLevelDisplay.Key(level)));
        }
    }

    [Theory]
    [InlineData(PublicSituationLevel.Niedrig, "Niedrig")]
    [InlineData(PublicSituationLevel.Erhoeht, "Erhoeht")]
    [InlineData(PublicSituationLevel.Hoch, "Hoch")]
    [InlineData(PublicSituationLevel.Kritisch, "Kritisch")]
    public void TheStoredKeysArePinned(PublicSituationLevel level, string key)
        // These four strings sit in the SystemSettings rows of every installation. A round-trip test cannot protect
        // them: rename one and Parse(Key(x)) still succeeds while every stored row stops parsing, /lage goes silent
        // and nothing turns red. Changing one of these is a data migration, not a rename.
        => Assert.Equal(key, PublicSituationLevelDisplay.Key(level));

    [Fact]
    public void TheGermanLabelIsNotTheStoredKey()
    {
        // The separation only means something if the label cannot be stored by accident, the way
        // WarnhinweisColourChoice keeps "Error" apart from "Rot". Erhoeht is the one pair where they differ, so it
        // is the only value that can show it. Deliberately asserted as a relationship and never as the label text:
        // pinning the wording here would re-create exactly the coupling the split removes, and the label is UI
        // copy that is meant to stay editable.
        var label = PublicSituationLevelDisplay.Name(PublicSituationLevel.Erhoeht);
        var key = PublicSituationLevelDisplay.Key(PublicSituationLevel.Erhoeht);

        Assert.NotEqual(label, key);
        Assert.Null(PublicSituationLevelDisplay.Parse(label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("7")]
    [InlineData("3")]
    [InlineData("Kritisch ")]
    [InlineData("kritisch")]
    [InlineData("Erhoht")]
    [InlineData("Erhöht")]
    [InlineData("Panisch")]
    public void AnythingElseIsUnknown(string? stored)
        => Assert.Null(PublicSituationLevelDisplay.Parse(stored));
}
