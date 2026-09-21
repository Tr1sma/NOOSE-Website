using NOOSE_Website.Navigation;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>What a keyboard shortcut resolves to. The component above it has no test, so this carries the rules.</summary>
public class ShortcutRoutesTests
{
    // ==================== the "g" chord ====================

    [Fact]
    public void EveryAreaLetterPointsAtAMenuEntryThatExists()
    {
        foreach (var (letter, key) in ShortcutRoutes.Areas)
        {
            // a typo here would make the key silently do nothing, which is the hardest kind of dead shortcut
            Assert.True(NavCatalog.ByKey(key) is not null, $"'g {letter}' zeigt auf den unbekannten Schlüssel '{key}'.");
        }
    }

    [Fact]
    public void NoTwoLettersLeadToTheSameArea()
        => Assert.Equal(ShortcutRoutes.Areas.Count, ShortcutRoutes.Areas.Values.Distinct(StringComparer.Ordinal).Count());

    [Fact]
    public void AnAreaLetterIsFoundWhateverTheCase()
    {
        Assert.Equal("personen", ShortcutRoutes.AreaKeyFor('p'));
        Assert.Equal("personen", ShortcutRoutes.AreaKeyFor('P'));
    }

    [Fact]
    public void AnUnboundLetterResolvesToNothing()
        => Assert.Null(ShortcutRoutes.AreaKeyFor('q'));

    // ==================== "n" ====================

    [Fact]
    public void NewAnswersOnABareListPage()
        => Assert.Equal("/personen/neu", ShortcutRoutes.NewRouteFor("personen"));

    [Fact]
    public void NewIgnoresTheQueryStringAndASlash()
    {
        Assert.Equal("/personen/neu", ShortcutRoutes.NewRouteFor("/personen/"));
        Assert.Equal("/personen/neu", ShortcutRoutes.NewRouteFor("personen?tab=alle"));
    }

    [Fact]
    public void NewStaysSilentOnADetailPage()
    {
        // the second segment is an id, not a command - offering "new" here would open a form nobody asked for
        Assert.Null(ShortcutRoutes.NewRouteFor("personen/abc-123"));
        Assert.Null(ShortcutRoutes.NewRouteFor("personen/abc-123/bearbeiten"));
    }

    [Fact]
    public void NewStaysSilentWhereNothingCanBeCreated()
    {
        Assert.Null(ShortcutRoutes.NewRouteFor("statistik"));
        Assert.Null(ShortcutRoutes.NewRouteFor(""));
        Assert.Null(ShortcutRoutes.NewRouteFor(null));
    }

    [Fact]
    public void NewNamesWhatItWouldCreate()
    {
        Assert.Equal("Personen-Akte", ShortcutRoutes.NewLabelFor("personen"));
        Assert.Null(ShortcutRoutes.NewLabelFor("statistik"));
    }

    // ==================== "e" ====================

    [Fact]
    public void EditAnswersOnARecord()
        => Assert.Equal("/personen/abc-123/bearbeiten", ShortcutRoutes.EditRouteFor("personen/abc-123"));

    [Fact]
    public void EditDoesNotStackASecondBearbeiten()
        => Assert.Null(ShortcutRoutes.EditRouteFor("personen/abc-123/bearbeiten"));

    [Fact]
    public void EditStaysSilentOnACreateForm()
        => Assert.Null(ShortcutRoutes.EditRouteFor("personen/neu"));

    [Fact]
    public void EditStaysSilentOnAListAndOnATypeWithoutAnEditor()
    {
        Assert.Null(ShortcutRoutes.EditRouteFor("personen"));
        Assert.Null(ShortcutRoutes.EditRouteFor("statistik/abc-123"));
        Assert.Null(ShortcutRoutes.EditRouteFor(null));
    }

    [Fact]
    public void EditIgnoresTheQueryString()
        => Assert.Equal("/vorgaenge/v1/bearbeiten", ShortcutRoutes.EditRouteFor("/vorgaenge/v1?tab=inhalt"));

    [Theory]
    [InlineData("besprechungen")]  // editor route wants HighestClassificationPage
    [InlineData("aufgaben")]       // editor wants creator or leadership
    [InlineData("kalender")]       // same
    [InlineData("aktivitaeten")]   // same
    [InlineData("brett")]          // editor wants the notice's own MayManage
    [InlineData("dokumente")]      // editor wants authorship and the secrecy level
    public void EditStaysSilentWhereTheEditorAsksMoreThanWriteAccess(string type)
    {
        // these have a /bearbeiten route, but the key only knows MayWrite - sending somebody there would
        // trade the page they were reading for a refusal, which is worse than the key doing nothing
        Assert.Null(ShortcutRoutes.EditRouteFor($"{type}/abc-123"));
    }
}
