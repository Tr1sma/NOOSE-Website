using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Unit tests for the pure phonetic/stemming/tokenizer helpers (no DB).</summary>
public sealed class SearchTokenizerTests
{
    [Theory]
    [InlineData("Maier", "Meyer")]
    [InlineData("Maier", "Mayr")]
    [InlineData("Maier", "Meier")]
    [InlineData("Meyer", "Mayr")]
    public void Cologne_SoundalikeSurnames_ShareCode(string a, string b)
    {
        Assert.Equal(ColognePhonetic.Encode(a), ColognePhonetic.Encode(b));
        Assert.NotEqual(string.Empty, ColognePhonetic.Encode(a));
    }

    [Fact]
    public void Cologne_MaierFamily_IsSixSeven()
    {
        Assert.Equal("67", ColognePhonetic.Encode("Maier"));
    }

    [Fact]
    public void Cologne_DistinctNames_DifferentCode()
    {
        Assert.NotEqual(ColognePhonetic.Encode("Müller"), ColognePhonetic.Encode("Schmidt"));
    }

    [Fact]
    public void Cologne_FoldsUmlauts()
    {
        // Müller vs Mueller — the folded 'ü' behaves like 'u' (a vowel, code 0)
        Assert.Equal(ColognePhonetic.Encode("Müller"), ColognePhonetic.Encode("Mueller"));
    }

    [Theory]
    [InlineData("Verhaftung", "Verhaftungen")]
    [InlineData("Ermittlung", "Ermittlungen")]
    public void Stemmer_SingularAndPlural_ShareStem(string singular, string plural)
    {
        Assert.Equal(GermanStemmer.Stem(singular), GermanStemmer.Stem(plural));
    }

    [Fact]
    public void Stemmer_ShortWord_Unchanged()
    {
        Assert.Equal("tat", GermanStemmer.Stem("Tat"));
    }

    [Fact]
    public void Tokenizer_PhoneticKeys_DedupesAcrossNameVariants()
    {
        var keys = SearchTokenizer.PhoneticKeys("Maier", "Meyer", "Mayr");
        Assert.Single(keys); // all collapse to one phonetic code
    }

    [Fact]
    public void Tokenizer_Stems_CoverAllTextFields()
    {
        var stems = SearchTokenizer.Stems("Verhaftungen", null, "Ermittlung");
        Assert.Contains(GermanStemmer.Stem("Verhaftung"), stems);
        Assert.Contains(GermanStemmer.Stem("Ermittlung"), stems);
    }

    [Fact]
    public void Tokenizer_Empty_ReturnsEmpty()
    {
        Assert.Empty(SearchTokenizer.PhoneticKeys(null, "", "   "));
        Assert.Empty(SearchTokenizer.Stems(null, ""));
    }
}
