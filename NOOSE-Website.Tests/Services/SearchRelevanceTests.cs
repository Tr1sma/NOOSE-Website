using NOOSE_Website.Models.Common;
using NOOSE_Website.Services;
using Xunit;

namespace NOOSE_Website.Tests.Services;

public class SearchRelevanceTests
{
    // SearchHit(Category, TargetId, Title, Snippet, CaseNumber, TargetType?)
    private static SearchHit Hit(string title, string snippet = "", string caseNumber = "")
        => new("Person", "id", title, snippet, caseNumber);

    // QuickHit(Category, TargetId, Name, CaseNumber)
    private static QuickHit Quick(string name, string caseNumber = "")
        => new("Person", "id", name, caseNumber);

    // ---- Rank: guard / no-op cases ----

    [Fact]
    public void Rank_SingleHit_ReturnsSameListUnchanged()
    {
        var hits = new List<SearchHit> { Hit("anything") };
        var result = SearchRelevance.Rank("query", hits);
        Assert.Same(hits, result);
    }

    [Fact]
    public void Rank_EmptyHits_ReturnsSameListUnchanged()
    {
        var hits = new List<SearchHit>();
        var result = SearchRelevance.Rank("query", hits);
        Assert.Same(hits, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rank_EmptyOrWhitespaceQuery_ReturnsSameListUnchanged(string? query)
    {
        var hits = new List<SearchHit> { Hit("beta"), Hit("alpha") };
        var result = SearchRelevance.Rank(query!, hits);
        Assert.Same(hits, result);
    }

    // ---- Rank: title score ladder ----

    [Fact]
    public void Rank_TitleMatchTypes_RankExactPrefixWordSubstringInOrder()
    {
        var exact = Hit("smith");            // t == q             -> 1000
        var prefix = Hit("smithsonian");     // StartsWith(q)      ->  600
        var word = Hit("john smith jr");     // whole-word match   ->  450
        var contains = Hit("blacksmithing"); // bare substring     ->  300

        var hits = new List<SearchHit> { contains, word, exact, prefix };
        var result = SearchRelevance.Rank("smith", hits);

        Assert.Equal(new[] { "smith", "smithsonian", "john smith jr", "blacksmithing" },
            result.Select(h => h.Title).ToArray());
    }

    [Fact]
    public void Rank_PrefixTitle_RanksAboveSubstringTitle()
    {
        var prefix = Hit("catalog");   // StartsWith("cat") -> 600
        var substring = Hit("scatter"); // substring only   -> 300

        var result = SearchRelevance.Rank("cat", new List<SearchHit> { substring, prefix });

        Assert.Equal("catalog", result[0].Title);
        Assert.Equal("scatter", result[1].Title);
    }

    [Fact]
    public void Rank_QueryIsTrimmedAndCaseInsensitive_MatchesExactTitleFirst()
    {
        var exact = Hit("Smith");       // lower == "smith" -> exact
        var prefix = Hit("Smithers");   // prefix

        var result = SearchRelevance.Rank("  SMITH  ", new List<SearchHit> { prefix, exact });

        Assert.Equal("Smith", result[0].Title);
        Assert.Equal("Smithers", result[1].Title);
    }

    // ---- Rank: case number and snippet fields ----

    [Fact]
    public void Rank_ExactCaseNumber_RanksAboveContainsCaseNumber()
    {
        var caseExact = Hit("Alpha", caseNumber: "noose");       // c == q -> 500
        var caseContains = Hit("Beta", caseNumber: "noose2026"); // substring -> 200

        var result = SearchRelevance.Rank("noose", new List<SearchHit> { caseContains, caseExact });

        Assert.Equal("noose", result[0].CaseNumber);
        Assert.Equal("noose2026", result[1].CaseNumber);
    }

    [Fact]
    public void Rank_SnippetMatch_RanksBelowTitleMatch()
    {
        var titleHit = Hit("foo");                                  // title exact -> 1000
        var snippetHit = Hit("bar", snippet: "the foo item");       // snippet only -> 80

        var result = SearchRelevance.Rank("foo", new List<SearchHit> { snippetHit, titleHit });

        Assert.Equal("foo", result[0].Title);
        Assert.Equal("bar", result[1].Title);
    }

    [Fact]
    public void Rank_CaseNumberOnlyMatch_RanksAboveZeroHit()
    {
        var caseHit = Hit("Something", caseNumber: "abc123"); // case exact -> 500
        var zeroHit = Hit("Nothing");                         // no field match -> 0

        var result = SearchRelevance.Rank("abc123", new List<SearchHit> { zeroHit, caseHit });

        Assert.Equal("Something", result[0].Title);
        Assert.Equal("Nothing", result[1].Title);
    }

    [Fact]
    public void Rank_EmptyFieldsHit_RanksLast()
    {
        var match = Hit("smith");
        var empty = Hit("", "", "");

        var result = SearchRelevance.Rank("smith", new List<SearchHit> { empty, match });

        Assert.Equal("smith", result[0].Title);
        Assert.Same(empty, result[1]);
    }

    // ---- Rank: multi-word token coverage ----

    [Fact]
    public void Rank_MultiWordQuery_OrdersByTokenCoverage()
    {
        var both = Hit("john smith"); // exact title + full coverage
        var one = Hit("john doe");    // one token covered
        var none = Hit("mary jane");  // no token covered

        var result = SearchRelevance.Rank("john smith", new List<SearchHit> { none, one, both });

        Assert.Equal(new[] { "john smith", "john doe", "mary jane" },
            result.Select(h => h.Title).ToArray());
    }

    // ---- Rank: fuzzy fallback ----

    [Fact]
    public void Rank_TypoTitle_RanksBelowLiteralButAboveNoMatch()
    {
        var literal = Hit("smith");  // exact -> 1000
        var typo = Hit("smyth");     // fuzzy fallback -> positive but small
        var noMatch = Hit("zzzzz");  // no literal, no fuzzy -> 0

        var result = SearchRelevance.Rank("smith", new List<SearchHit> { typo, noMatch, literal });

        Assert.Equal(new[] { "smith", "smyth", "zzzzz" },
            result.Select(h => h.Title).ToArray());
    }

    // ---- Rank: ordering stability / tie-break ----

    [Fact]
    public void Rank_NoMatches_OrdersByTitleAscending()
    {
        var cherry = Hit("cherry");
        var banana = Hit("banana");
        var apple = Hit("apple");

        var result = SearchRelevance.Rank("zzzzz", new List<SearchHit> { cherry, banana, apple });

        Assert.Equal(new[] { "apple", "banana", "cherry" },
            result.Select(h => h.Title).ToArray());
    }

    [Fact]
    public void Rank_EqualScores_TieBreaksByTitleAscending()
    {
        var beta = Hit("Beta", caseNumber: "match");   // 500
        var alpha = Hit("Alpha", caseNumber: "match"); // 500

        var result = SearchRelevance.Rank("match", new List<SearchHit> { beta, alpha });

        Assert.Same(alpha, result[0]);
        Assert.Same(beta, result[1]);
    }

    [Fact]
    public void Rank_WhenRanking_ReturnsNewListWithoutMutatingInput()
    {
        var first = Hit("blacksmithing"); // low score
        var second = Hit("smith");        // high score
        var hits = new List<SearchHit> { first, second };

        var result = SearchRelevance.Rank("smith", hits);

        Assert.NotSame(hits, result);
        // input order untouched
        Assert.Same(first, hits[0]);
        Assert.Same(second, hits[1]);
        // output reordered by score
        Assert.Same(second, result[0]);
        Assert.Same(first, result[1]);
    }

    // ---- RankQuick: guard / no-op cases ----

    [Fact]
    public void RankQuick_SingleHit_ReturnsSameListUnchanged()
    {
        var hits = new List<QuickHit> { Quick("anything") };
        var result = SearchRelevance.RankQuick("query", hits);
        Assert.Same(hits, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RankQuick_EmptyOrWhitespaceQuery_ReturnsSameListUnchanged(string? query)
    {
        var hits = new List<QuickHit> { Quick("beta"), Quick("alpha") };
        var result = SearchRelevance.RankQuick(query!, hits);
        Assert.Same(hits, result);
    }

    // ---- RankQuick: scoring / ordering ----

    [Fact]
    public void RankQuick_NameMatchTypes_RankExactPrefixSubstringInOrder()
    {
        var exact = Quick("smith");      // 1000
        var prefix = Quick("smithy");    //  600
        var substr = Quick("locksmith"); //  300

        var result = SearchRelevance.RankQuick("smith", new List<QuickHit> { substr, exact, prefix });

        Assert.Equal(new[] { "smith", "smithy", "locksmith" },
            result.Select(h => h.Name).ToArray());
    }

    [Fact]
    public void RankQuick_ScoresNameHigherThanCaseNumber()
    {
        var nameExact = Quick("alpha");                  // name exact -> 1000
        var caseExact = Quick("zzz", caseNumber: "alpha"); // case exact -> 500

        var result = SearchRelevance.RankQuick("alpha", new List<QuickHit> { caseExact, nameExact });

        Assert.Equal("alpha", result[0].Name);
        Assert.Equal("zzz", result[1].Name);
    }

    [Fact]
    public void RankQuick_EqualScores_TieBreaksByNameAscending()
    {
        var zed = Quick("Zed", caseNumber: "match");   // 500
        var ace = Quick("Ace", caseNumber: "match");   // 500

        var result = SearchRelevance.RankQuick("match", new List<QuickHit> { zed, ace });

        Assert.Same(ace, result[0]);
        Assert.Same(zed, result[1]);
    }
}
