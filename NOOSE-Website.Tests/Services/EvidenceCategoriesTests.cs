using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The category filter sentinel and its German label.</summary>
public class EvidenceCategoriesTests
{
    [Fact]
    public void Null_means_no_filter()
    {
        Assert.False(EvidenceCategories.IsNone(null));
    }

    [Fact]
    public void Empty_string_asks_for_the_uncategorised()
    {
        Assert.Empty(EvidenceCategories.None);
        Assert.True(EvidenceCategories.IsNone(EvidenceCategories.None));
    }

    [Fact]
    public void A_real_category_is_not_the_sentinel()
    {
        Assert.False(EvidenceCategories.IsNone("Drogen"));
        // whitespace is not the sentinel: catalog values are trimmed, so it can only come from a caller bug
        Assert.False(EvidenceCategories.IsNone(" "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Label_falls_back_for_missing_values(string? category)
    {
        Assert.Equal("Ohne Kategorie", EvidenceCategories.Label(category));
    }

    [Fact]
    public void Label_passes_a_real_category_through()
    {
        Assert.Equal("Drogen", EvidenceCategories.Label("Drogen"));
    }

    [Fact]
    public void Matches_null_filter_accepts_everything()
    {
        Assert.True(EvidenceCategories.Matches("Drogen", null));
        Assert.True(EvidenceCategories.Matches(null, null));
    }

    [Fact]
    public void Matches_none_filter_accepts_only_the_uncategorised()
    {
        Assert.True(EvidenceCategories.Matches(null, EvidenceCategories.None));
        Assert.True(EvidenceCategories.Matches("", EvidenceCategories.None));
        Assert.False(EvidenceCategories.Matches("Drogen", EvidenceCategories.None));
    }

    [Fact]
    public void Matches_ignores_case_like_the_MySQL_collation()
    {
        // the catalog keeps the first-seen casing while an item stores what was typed
        Assert.True(EvidenceCategories.Matches("drogen", "Drogen"));
        Assert.True(EvidenceCategories.Matches("DROGEN", "Drogen"));
        Assert.False(EvidenceCategories.Matches("Waffen", "Drogen"));
    }

    [Fact]
    public void Matches_uncategorised_item_against_a_real_filter_is_false()
    {
        Assert.False(EvidenceCategories.Matches(null, "Drogen"));
        Assert.False(EvidenceCategories.Matches("   ", "Drogen"));
    }
}
