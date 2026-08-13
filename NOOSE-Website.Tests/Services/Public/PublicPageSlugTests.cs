using NOOSE_Website.Infrastructure;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The slug rules; a public page's address is validated on write, so this is the whole gate.</summary>
public class PublicPageSlugTests
{
    [Theory]
    [InlineData("auftrag")]
    [InlineData("faq")]
    [InlineData("unsere-befugnisse")]
    [InlineData("paragraph-129a")]
    [InlineData("ab")]
    public void IsValid_AcceptsARoutableSlug(string slug)
        => Assert.True(PublicPageSlug.IsValid(slug));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Auftrag")]
    [InlineData("unser auftrag")]
    [InlineData("unser_auftrag")]
    [InlineData("-auftrag")]
    [InlineData("auftrag-")]
    [InlineData("unser--auftrag")]
    [InlineData("über-uns")]
    [InlineData("auftrag/unter")]
    [InlineData("auftrag?x=1")]
    [InlineData("../etc")]
    [InlineData("<script>")]
    public void IsValid_RejectsAnythingElse(string? slug)
        => Assert.False(PublicPageSlug.IsValid(slug));

    [Fact]
    public void IsValid_RejectsAnOverlongSlug()
    {
        Assert.True(PublicPageSlug.IsValid(new string('a', PublicPageSlug.MaxLength)));
        Assert.False(PublicPageSlug.IsValid(new string('a', PublicPageSlug.MaxLength + 1)));
    }

    [Theory]
    [InlineData("Unser Auftrag", "unser-auftrag")]
    [InlineData("Häufige Fragen", "haeufige-fragen")]
    [InlineData("Zuständigkeiten", "zustaendigkeiten")]
    [InlineData("Über uns", "ueber-uns")]
    [InlineData("Maßnahmen", "massnahmen")]
    [InlineData("§ 129a StGB", "129a-stgb")]
    [InlineData("  Auftrag  ", "auftrag")]
    [InlineData("A---B", "a-b")]
    [InlineData("FAQ", "faq")]
    public void Normalize_FoldsAGermanTitleIntoASlug(string input, string expected)
        => Assert.Equal(expected, PublicPageSlug.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("---")]
    public void Normalize_WithNothingUsable_ReturnsEmpty(string? input)
        => Assert.Equal(string.Empty, PublicPageSlug.Normalize(input));

    [Fact]
    public void Normalize_IsIdempotentOnAValidSlug()
    {
        const string slug = "unsere-befugnisse";
        Assert.Equal(slug, PublicPageSlug.Normalize(slug));
        Assert.Equal(slug, PublicPageSlug.Normalize(PublicPageSlug.Normalize(slug)));
    }

    [Fact]
    public void Normalize_CutsToTheStoredLength_WithoutLeavingATrailingHyphen()
    {
        var result = PublicPageSlug.Normalize(new string('a', 70) + " " + new string('b', 10));

        Assert.Equal(PublicPageSlug.MaxLength, result.Length);
        Assert.True(PublicPageSlug.IsValid(result));
    }

    [Fact]
    public void Normalize_LongTitleThatWouldEndOnAHyphen_StaysValid()
    {
        // the cut lands exactly on the separator here; trimming afterwards is what keeps the result routable
        var result = PublicPageSlug.Normalize(new string('a', PublicPageSlug.MaxLength - 1) + " ende");

        Assert.Equal(new string('a', PublicPageSlug.MaxLength - 1), result);
        Assert.True(PublicPageSlug.IsValid(result));
    }

    [Fact]
    public void EveryStarterPage_HasARoutableSlug()
    {
        // the seeder writes these straight into the table, bypassing the service's validation
        Assert.All(PublicPageSeeder.Starters, starter => Assert.True(PublicPageSlug.IsValid(starter.Slug)));
    }

    [Fact]
    public void StarterSlugs_AreUnique()
    {
        var slugs = PublicPageSeeder.Starters.Select(s => s.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryStarterPage_PicksAnIconFromTheAllowlist()
    {
        Assert.All(PublicPageSeeder.Starters, starter => Assert.True(PublicModules.IsKnownIcon(starter.IconName)));
    }

    [Fact]
    public void EveryStarterPage_CarriesTitleMenuTitleAndABody()
    {
        Assert.All(PublicPageSeeder.Starters, starter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(starter.Title));
            Assert.False(string.IsNullOrWhiteSpace(starter.MenuTitle));
            Assert.False(string.IsNullOrWhiteSpace(starter.DraftHtml));
        });
    }
}
