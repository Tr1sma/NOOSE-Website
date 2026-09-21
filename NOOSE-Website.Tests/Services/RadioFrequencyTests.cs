using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Frequency spelling: the one thing the reverse lookup depends on.</summary>
public class RadioFrequencyTests
{
    [Fact]
    public void Normalize_turns_the_German_comma_into_the_stored_dot()
        => Assert.Equal("411.7", RadioFrequency.Normalize("411,7"));

    [Fact]
    public void Normalize_leaves_an_already_dotted_frequency_alone()
        => Assert.Equal("411.7", RadioFrequency.Normalize("411.7"));

    [Fact]
    public void Normalize_trims_the_surrounding_whitespace()
        => Assert.Equal("98.4", RadioFrequency.Normalize("  98,4  "));

    [Fact]
    public void Normalize_answers_empty_for_nothing()
    {
        Assert.Equal(string.Empty, RadioFrequency.Normalize(null));
        Assert.Equal(string.Empty, RadioFrequency.Normalize("   "));
    }

    [Fact]
    public void Normalize_leaves_a_word_untouched()
        => Assert.Equal("TRU", RadioFrequency.Normalize(" TRU "));

    [Fact]
    public void Comma_writes_the_same_needle_the_other_way()
    {
        // a column nothing normalised on write has to be compared against both spellings of the query
        Assert.Equal("411,7", RadioFrequency.Comma("411.7"));
        Assert.Equal("411,7", RadioFrequency.Comma("411,7"));
        Assert.Equal("TRU", RadioFrequency.Comma(" TRU "));
        Assert.Equal(string.Empty, RadioFrequency.Comma(null));
    }
}
