using NOOSE_Website.Models.Navigation;

namespace NOOSE_Website.Services;

/// <summary>The first steps a new agent is asked to take, and how far they have got.</summary>
/// <remarks>
/// Static and pure: every answer comes out of the preferences blob the drawer has already loaded and cached, so
/// the dashboard can show the list without a single extra query. That is the whole design constraint - a step
/// whose "done" needed counting rows would cost a query on every dashboard render.
/// <para>
/// Five steps are stamped where they happen (<see cref="INavPreferencesService.MarkOnboardingStepAsync"/>); the
/// sixth is derived, because the preferences themselves already say whether the menu was touched.
/// </para>
/// </remarks>
public static class Onboarding
{
    public const string StepProfile = "profil";
    public const string StepHandbook = "handbuch";
    public const string StepSearch = "suche";
    public const string StepRecord = "akte";

    /// <summary>Prefix of a per-chapter marker; the reading step counts how many distinct ones are set.</summary>
    public const string ChapterPrefix = "kapitel:";

    /// <summary>How many chapters count as "found your way around the handbook".</summary>
    public const int ChaptersToRead = 3;

    /// <param name="Key">Stable handle, also the marker written when the step is reached.</param>
    /// <param name="Href">Where the agent goes to do it.</param>
    public sealed record Step(string Key, string Title, string Text, string Icon, string Href, bool Done);

    /// <summary>Marker for one chapter of the handbook.</summary>
    public static string ChapterStep(string chapterSlug) => ChapterPrefix + chapterSlug;

    public static IReadOnlyList<Step> Steps(NavPreferences prefs)
    {
        var done = prefs.OnboardingDone;
        var chapters = ChaptersRead(prefs);

        return
        [
            new(StepProfile, "Profil angesehen",
                "Schau nach, unter welchem Codenamen dich alle anderen sehen.",
                "AccountCircle", "/profil", done.Contains(StepProfile)),
            new(StepHandbook, "Handbuch geöffnet",
                "Das Handbuch erklärt jede Seite. Oben steht ein eigenes Suchfeld.",
                "MenuBook", "/handbuch", done.Contains(StepHandbook)),
            new("kapitel", $"{ChaptersToRead} Kapitel gelesen",
                $"Bisher {chapters} von {ChaptersToRead}. Fang mit „Erste Schritte“ an.",
                "AutoStories", "/handbuch", chapters >= ChaptersToRead),
            new(StepSearch, "Einmal gesucht",
                "Die Suche geht über den ganzen Bestand und verzeiht Tippfehler. Strg+K überall.",
                "Search", "/suche", done.Contains(StepSearch)),
            new(StepRecord, "Eine Akte geöffnet",
                "Such dir eine Personen- oder Fraktionsakte und sieh dir ihre Abschnitte an.",
                "FolderShared", "/personen", done.Contains(StepRecord)),
            new("menue", "Menü angepasst",
                "Blende aus, was du nie brauchst, und hefte an, was du täglich öffnest.",
                "Tune", "/dashboard", MenuTouched(prefs)),
        ];
    }

    public static int DoneCount(NavPreferences prefs) => Steps(prefs).Count(s => s.Done);

    public static int TotalCount(NavPreferences prefs) => Steps(prefs).Count;

    public static bool IsComplete(NavPreferences prefs) => Steps(prefs).All(s => s.Done);

    private static int ChaptersRead(NavPreferences prefs)
        => prefs.OnboardingDone.Count(k => k.StartsWith(ChapterPrefix, StringComparison.Ordinal));

    // derived rather than stamped: the preferences are the answer, so there is nothing to write down. The
    // price is that resetting the menu to default un-ticks it again, which is arguably the honest reading.
    private static bool MenuTouched(NavPreferences prefs)
        => prefs.Favorites.Count > 0
           || prefs.HiddenKeys.Count > 0
           || prefs.Order.Count > 0
           || prefs.StartRoute is not null;
}
