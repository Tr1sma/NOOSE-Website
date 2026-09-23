using NOOSE_Website.Models.Navigation;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The first-steps checklist: what counts as done, and what the six steps are read out of.</summary>
public sealed class OnboardingTests
{
    private static NavPreferences Fresh() => new();

    private static NavPreferences With(params string[] markers)
    {
        var prefs = new NavPreferences();
        foreach (var m in markers)
        {
            prefs.OnboardingDone.Add(m);
        }
        return prefs;
    }

    private static bool Done(NavPreferences prefs, string key)
        => Onboarding.Steps(prefs).Single(s => s.Key == key).Done;

    [Fact]
    public void A_fresh_agent_has_nothing_done()
    {
        var prefs = Fresh();

        Assert.Equal(0, Onboarding.DoneCount(prefs));
        Assert.Equal(6, Onboarding.TotalCount(prefs));
        Assert.False(Onboarding.IsComplete(prefs));
    }

    [Theory]
    [InlineData(Onboarding.StepProfile)]
    [InlineData(Onboarding.StepHandbook)]
    [InlineData(Onboarding.StepSearch)]
    [InlineData(Onboarding.StepRecord)]
    public void A_marker_completes_its_own_step_and_no_other(string marker)
    {
        var prefs = With(marker);

        Assert.True(Done(prefs, marker));
        Assert.Equal(1, Onboarding.DoneCount(prefs));
    }

    [Fact]
    public void A_marker_is_idempotent()
    {
        var prefs = With(Onboarding.StepSearch, Onboarding.StepSearch);

        Assert.Equal(1, Onboarding.DoneCount(prefs));
    }

    // --- the reading step -------------------------------------------------

    [Fact]
    public void Two_chapters_are_not_enough()
    {
        var prefs = With(Onboarding.ChapterStep("erste-schritte"), Onboarding.ChapterStep("akten-fuehren"));

        Assert.False(Done(prefs, "kapitel"));
    }

    [Fact]
    public void Three_chapters_complete_the_reading_step()
    {
        var prefs = With(
            Onboarding.ChapterStep("erste-schritte"),
            Onboarding.ChapterStep("akten-fuehren"),
            Onboarding.ChapterStep("ermitteln"));

        Assert.True(Done(prefs, "kapitel"));
    }

    /// <summary>The same chapter twice is one chapter; the marker set is what makes that automatic.</summary>
    [Fact]
    public void The_same_chapter_read_three_times_is_still_one()
    {
        var prefs = new NavPreferences();
        for (var i = 0; i < 3; i++)
        {
            prefs.OnboardingDone.Add(Onboarding.ChapterStep("erste-schritte"));
        }

        Assert.False(Done(prefs, "kapitel"));
    }

    /// <summary>A chapter marker must not be mistaken for one of the plain steps.</summary>
    [Fact]
    public void A_chapter_marker_completes_no_plain_step()
    {
        var prefs = With(Onboarding.ChapterStep("erste-schritte"));

        Assert.False(Done(prefs, Onboarding.StepHandbook));
        Assert.Equal(0, Onboarding.DoneCount(prefs));
    }

    // --- the derived step -------------------------------------------------

    public static TheoryData<NavPreferences> TouchedMenus() =>
    [
        new NavPreferences { StartRoute = "/personen" },
        new NavPreferences { HiddenKeys = ["graph"] },
        new NavPreferences { Order = ["personen", "fraktionen"] },
        new NavPreferences { Favorites = [new NavFavorite("page", "personen", null, null, "Personen", "/personen", "x")] },
    ];

    [Theory]
    [MemberData(nameof(TouchedMenus))]
    public void Any_menu_change_completes_the_menu_step(NavPreferences prefs)
        => Assert.True(Done(prefs, "menue"));

    [Fact]
    public void An_untouched_menu_leaves_the_step_open()
        => Assert.False(Done(Fresh(), "menue"));

    [Fact]
    public void A_saved_list_view_alone_leaves_the_menu_step_open()
        // the step asks for hiding and pinning; remembering a filter is neither, which is why views are not favorites
        => Assert.False(Done(new NavPreferences
        {
            SavedViews = [new SavedView("v1", "Rot", "/personen?aktualitaet=Red", "x")],
        }, "menue"));

    // --- completion -------------------------------------------------------

    [Fact]
    public void All_six_together_complete_the_list()
    {
        var prefs = With(
            Onboarding.StepProfile, Onboarding.StepHandbook, Onboarding.StepSearch, Onboarding.StepRecord,
            Onboarding.ChapterStep("a"), Onboarding.ChapterStep("b"), Onboarding.ChapterStep("c"));
        prefs.StartRoute = "/personen";

        Assert.True(Onboarding.IsComplete(prefs));
        Assert.Equal(6, Onboarding.DoneCount(prefs));
    }

    /// <summary>Every step has to lead somewhere: the card renders the title as a link to it.</summary>
    [Fact]
    public void Every_step_names_a_route_and_an_icon()
    {
        Assert.All(Onboarding.Steps(Fresh()), s =>
        {
            // a step may have no route - the menu customiser is a dialog - but a route that IS named has to be
            // one, and it must not point at the dashboard, which is the page the checklist itself sits on
            if (s.Href is not null)
            {
                Assert.StartsWith("/", s.Href, StringComparison.Ordinal);
                Assert.NotEqual("/dashboard", s.Href);
            }
            Assert.False(string.IsNullOrWhiteSpace(s.Icon));
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.False(string.IsNullOrWhiteSpace(s.Text));
        });
    }

    [Fact]
    public void Step_keys_are_unique()
    {
        var keys = Onboarding.Steps(Fresh()).Select(s => s.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }
}
