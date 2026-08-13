using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>Consistency of the module catalog: it is the only place that decides which switches exist.</summary>
public class PublicModulesCatalogTests
{
    [Fact]
    public void Keys_AreUnique()
    {
        var duplicates = PublicModules.All
            .GroupBy(m => m.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.True(duplicates.Length == 0, "Doppelte Modul-Schlüssel: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void Keys_FitTheColumn()
    {
        // Schluessel is varchar(64); a longer key would only fail on MySQL, not in these tests
        Assert.All(PublicModules.All, m => Assert.InRange(m.Key.Length, 1, 64));
    }

    [Fact]
    public void Every_module_carries_label_description_icon_and_offline_text()
    {
        Assert.All(PublicModules.All, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Label), m.Key);
            Assert.False(string.IsNullOrWhiteSpace(m.Description), m.Key);
            Assert.False(string.IsNullOrWhiteSpace(m.Icon), m.Key);
            Assert.False(string.IsNullOrWhiteSpace(m.DefaultOfflineText), m.Key);
        });
    }

    [Fact]
    public void Nav_routes_are_absolute_and_unique()
    {
        var routes = PublicModules.All
            .Where(m => m.NavRoute is not null)
            .Select(m => m.NavRoute!)
            .ToList();

        Assert.All(routes, r => Assert.StartsWith("/", r, StringComparison.Ordinal));
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Only_a_built_module_may_default_to_on()
    {
        // nothing goes live by deploying: a key whose pages do not exist yet must start off
        var premature = PublicModules.All
            .Where(m => m.DefaultEnabled && !m.Available)
            .Select(m => m.Key)
            .ToArray();

        Assert.True(premature.Length == 0,
            "Standardmäßig an, obwohl die Seiten fehlen: " + string.Join(", ", premature));
    }

    [Fact]
    public void Every_nav_route_counts_as_public_for_the_crawler()
    {
        // a module can otherwise be public in the nav and noindex to the crawler at the same time
        var mismatched = PublicModules.All
            .Where(m => m.NavRoute is not null && !PublicRoutes.IsPublic(m.NavRoute))
            .Select(m => m.NavRoute!)
            .ToArray();

        Assert.True(mismatched.Length == 0, "Nicht als öffentlich erkannt: " + string.Join(", ", mismatched));
    }

    [Fact]
    public void Icon_choices_are_unique_and_usable()
    {
        var names = PublicModules.IconChoices.Select(c => c.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PublicModules.IconChoices, c =>
        {
            Assert.InRange(c.Name.Length, 1, 64);
            Assert.False(string.IsNullOrWhiteSpace(c.Label));
            Assert.False(string.IsNullOrWhiteSpace(c.Icon));
        });
    }

    [Fact]
    public void IconFor_UnknownName_KeepsTheFallback()
    {
        Assert.Equal("fallback", PublicModules.IconFor("<script>", "fallback"));
        Assert.Equal("fallback", PublicModules.IconFor(null, "fallback"));
        Assert.Equal("fallback", PublicModules.IconFor("   ", "fallback"));
    }

    [Fact]
    public void IconFor_KnownName_ResolvesToTheIcon()
    {
        var choice = PublicModules.IconChoices[0];

        Assert.Equal(choice.Icon, PublicModules.IconFor(choice.Name, "fallback"));
    }

    [Fact]
    public void IsKnownIcon_OnlyAcceptsTheAllowlist()
    {
        Assert.True(PublicModules.IsKnownIcon(PublicModules.IconChoices[0].Name));
        Assert.False(PublicModules.IsKnownIcon("Nope"));
        Assert.False(PublicModules.IsKnownIcon(null));
    }

    [Fact]
    public void Find_IsExactAndCaseSensitive()
    {
        Assert.NotNull(PublicModules.Find(PublicModules.Wanted));
        Assert.Null(PublicModules.Find("fahndung"));
        Assert.Null(PublicModules.Find(null));
    }
}
