using NOOSE_Website.Models.Navigation;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>An agent's saved list views: same name, how many, and which list a view belongs to.</summary>
/// <remarks>The header menu, the drawer and the palette are Razor and run in no test, so the rules carry it here.</remarks>
public class SavedViewRulesTests
{
    private const string Icon = "icon";

    private static List<SavedView> Views(params (string Label, string Route)[] rows)
        => rows.Select((r, i) => new SavedView($"id{i}", r.Label, r.Route, Icon)).ToList();

    // ==================== saving ====================

    [Fact]
    public void A_new_name_is_added_at_the_end_with_an_id_of_its_own()
    {
        var views = Views(("Rot", "/personen?aktualitaet=Red"));

        var outcome = SavedViewRules.Add(views, "Verdachtsfälle", "/personen?einstufung=SuspicionCase", Icon);

        Assert.Equal(SavedViewOutcome.Added, outcome);
        Assert.Equal(2, views.Count);
        Assert.Equal("Verdachtsfälle", views[1].Label);
        Assert.False(string.IsNullOrWhiteSpace(views[1].Id));
        Assert.NotEqual(views[0].Id, views[1].Id);
    }

    [Theory]
    [InlineData("Rot")]
    [InlineData("rot")]
    [InlineData("  ROT  ")]
    public void The_same_name_overwrites_and_keeps_its_place_and_id(string again)
    {
        var views = Views(("Rot", "/personen?aktualitaet=Red"), ("Andere", "/fraktionen?q=x"));

        var outcome = SavedViewRules.Add(views, again, "/personen?aktualitaet=Red&einstufung=SuspicionCase", Icon);

        Assert.Equal(SavedViewOutcome.Replaced, outcome);
        Assert.Equal(2, views.Count);
        Assert.Equal("id0", views[0].Id);
        Assert.Equal("/personen?aktualitaet=Red&einstufung=SuspicionCase", views[0].Route);
    }

    [Fact]
    public void The_same_name_on_another_list_is_a_view_of_its_own()
    {
        // a global name would carry the people view off to the factions list, and the people menu would lose it
        var views = Views(("Rot", "/personen?aktualitaet=Red"));

        var outcome = SavedViewRules.Add(views, "Rot", "/fraktionen?aktualitaet=Red", Icon);

        Assert.Equal(SavedViewOutcome.Added, outcome);
        Assert.Equal(2, views.Count);
        Assert.Equal("/personen?aktualitaet=Red", views[0].Route);
        Assert.Single(SavedViewRules.ForRoute(views, "/personen"));
        Assert.Single(SavedViewRules.ForRoute(views, "/fraktionen"));
    }

    [Fact]
    public void The_same_name_on_the_same_list_overwrites_whatever_its_filters()
        // the list is the scope, not the query: a corrected filter replaces the old one
        => Assert.Equal(SavedViewOutcome.Replaced, SavedViewRules.Add(
            Views(("Rot", "/personen?aktualitaet=Red")), "ROT", "/Personen?einstufung=SuspicionCase", Icon));

    [Fact]
    public void The_cap_stops_a_new_name_and_changes_nothing()
    {
        var views = Enumerable.Range(0, SavedViewRules.Cap)
            .Select(i => new SavedView($"id{i}", $"Ansicht {i}", "/personen?q=" + i, Icon)).ToList();

        var outcome = SavedViewRules.Add(views, "Eine zu viel", "/personen?q=x", Icon);

        Assert.Equal(SavedViewOutcome.Full, outcome);
        Assert.Equal(SavedViewRules.Cap, views.Count);
        Assert.DoesNotContain(views, v => v.Label == "Eine zu viel");
    }

    [Fact]
    public void A_full_list_still_lets_a_name_be_replaced()
    {
        // replacing needs no free slot; without this a full agent could not correct a single view
        var views = Enumerable.Range(0, SavedViewRules.Cap)
            .Select(i => new SavedView($"id{i}", $"Ansicht {i}", "/personen?q=" + i, Icon)).ToList();

        var outcome = SavedViewRules.Add(views, "Ansicht 3", "/personen?q=neu", Icon);

        Assert.Equal(SavedViewOutcome.Replaced, outcome);
        Assert.Equal("/personen?q=neu", views[3].Route);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void A_view_without_a_name_is_refused(string? name)
    {
        var views = new List<SavedView>();

        Assert.Equal(SavedViewOutcome.Invalid, SavedViewRules.Add(views, name, "/personen?q=x", Icon));
        Assert.Empty(views);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("personen?q=x")]
    [InlineData("//evil.example/personen")]
    [InlineData("/\\evil.example")]
    [InlineData("https://evil.example/personen")]
    [InlineData("/personen?q=a\nb")]
    public void A_route_that_would_leave_the_site_is_refused(string? route)
    {
        // drawer and palette put the route straight into an href
        var views = new List<SavedView>();

        Assert.Equal(SavedViewOutcome.Invalid, SavedViewRules.Add(views, "Name", route, Icon));
        Assert.Empty(views);
    }

    [Fact]
    public void An_overlong_route_is_refused_rather_than_cut()
    {
        // cutting an encoded query can end inside an escape and break the address
        var route = "/personen?q=" + new string('a', SavedViewRules.MaxRouteLength);

        Assert.Equal(SavedViewOutcome.Invalid, SavedViewRules.Add([], "Name", route, Icon));
    }

    [Fact]
    public void A_name_is_trimmed_collapsed_and_cut_to_the_limit()
    {
        Assert.Equal("Rot und Verdacht", SavedViewRules.Normalise("  Rot   und\tVerdacht "));
        Assert.Equal(SavedViewRules.MaxLabelLength, SavedViewRules.Normalise(new string('a', 200)).Length);
    }

    // ==================== removing ====================

    [Fact]
    public void Removing_takes_exactly_the_view_with_that_id()
    {
        var views = Views(("Rot", "/personen?aktualitaet=Red"), ("Andere", "/fraktionen?q=x"));

        Assert.True(SavedViewRules.Remove(views, "id0"));
        Assert.Equal("Andere", Assert.Single(views).Label);
    }

    [Theory]
    [InlineData("gibt-es-nicht")]
    [InlineData("")]
    [InlineData(null)]
    public void Removing_an_unknown_id_changes_nothing(string? id)
    {
        var views = Views(("Rot", "/personen?aktualitaet=Red"));

        Assert.False(SavedViewRules.Remove(views, id));
        Assert.Single(views);
    }

    // ==================== which list owns a view ====================

    [Fact]
    public void A_list_sees_its_own_views_and_not_the_ones_of_a_longer_route()
    {
        // the reason the path is compared exactly: /personengruppen starts with /personen
        var views = Views(
            ("Personen rot", "/personen?aktualitaet=Red"),
            ("Gruppen rot", "/personengruppen?aktualitaet=Red"),
            ("Eine Akte", "/personen/123?tab=doks"));

        var mine = SavedViewRules.ForRoute(views, "/personen");

        Assert.Equal("Personen rot", Assert.Single(mine).Label);
    }

    [Fact]
    public void A_list_finds_its_views_whatever_the_case_and_trailing_slash()
    {
        var views = Views(("Rot", "/Personen/?aktualitaet=Red"));

        Assert.Single(SavedViewRules.ForRoute(views, "/personen"));
        Assert.Single(SavedViewRules.ForRoute(views, "personen/"));
    }

    [Theory]
    [InlineData("personen?q=1", "/personen")]
    [InlineData("/personen/", "/personen")]
    [InlineData("/personen#oben", "/personen")]
    [InlineData("/personen?q=a/b", "/personen")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    public void The_path_of_a_route_drops_query_fragment_and_slashes(string? route, string expected)
        // the base-relative form NavigationManager hands out has no leading slash
        => Assert.Equal(expected, SavedViewRules.PathOf(route));

    [Fact]
    public void A_taken_name_is_recognised_in_any_case()
    {
        var views = Views(("Verdachtsfälle", "/personen?einstufung=SuspicionCase"));

        Assert.True(SavedViewRules.IsTaken(views, " verdachtsfälle "));
        Assert.False(SavedViewRules.IsTaken(views, "Rot"));
        Assert.False(SavedViewRules.IsTaken(views, "   "));
    }
}
