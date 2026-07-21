using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

public class TestGradingTests
{
    // ---------- GradeMultipleChoice ----------

    [Fact]
    public void GradeMultipleChoice_null_returnsNull()
    {
        Assert.Null(TestGrading.GradeMultipleChoice(null));
    }

    [Fact]
    public void GradeMultipleChoice_correctOption_returnsTrue()
    {
        var chosen = new BewerbungTestOption { IsCorrect = true };
        Assert.Equal(true, TestGrading.GradeMultipleChoice(chosen));
    }

    [Fact]
    public void GradeMultipleChoice_wrongOption_returnsFalse()
    {
        var chosen = new BewerbungTestOption { IsCorrect = false };
        Assert.Equal(false, TestGrading.GradeMultipleChoice(chosen));
    }

    // ---------- GradeYesNo ----------

    [Theory]
    [InlineData(null)]
    [InlineData("Ja")]
    [InlineData("Nein")]
    [InlineData("garbage")]
    [InlineData("")]
    public void GradeYesNo_noCorrectDefined_returnsNull(string? answer)
    {
        Assert.Null(TestGrading.GradeYesNo(answer, null));
    }

    [Theory]
    // answer, correctYesNo, expected
    [InlineData("Ja", true, true)]
    [InlineData("Ja", false, false)]
    [InlineData("Nein", false, true)]
    [InlineData("Nein", true, false)]
    public void GradeYesNo_matchesAgainstCorrect(string answer, bool correct, bool expected)
    {
        Assert.Equal(expected, TestGrading.GradeYesNo(answer, correct));
    }

    [Theory]
    [InlineData("ja")]
    [InlineData("JA")]
    [InlineData("jA")]
    [InlineData("  Ja  ")]
    [InlineData("\tJa\n")]
    public void GradeYesNo_yesCaseAndWhitespaceInsensitive_returnsTrueWhenCorrectIsYes(string answer)
    {
        Assert.Equal(true, TestGrading.GradeYesNo(answer, true));
    }

    [Theory]
    [InlineData("nein")]
    [InlineData("NEIN")]
    [InlineData("  Nein ")]
    public void GradeYesNo_noCaseAndWhitespaceInsensitive_returnsTrueWhenCorrectIsNo(string answer)
    {
        Assert.Equal(true, TestGrading.GradeYesNo(answer, false));
    }

    [Theory]
    // Unparseable / unanswered counts as wrong (never null when a correct answer exists).
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Yes", true)]
    [InlineData("maybe", false)]
    public void GradeYesNo_unparseableAnswer_returnsFalseNotNull(string? answer, bool correct)
    {
        var result = TestGrading.GradeYesNo(answer, correct);
        Assert.NotNull(result);
        Assert.Equal(false, result);
    }

    // ---------- GradeFreeText: no keywords ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\r\t")]
    public void GradeFreeText_noKeywords_returnsNullCorrectAndEmptyLists(string? keywords)
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText("some answer", keywords, null);
        Assert.Null(correct);
        Assert.Empty(matched);
        Assert.Empty(missed);
    }

    [Fact]
    public void GradeFreeText_keywordsAllDelimitersOnly_treatedAsNoKeywords()
    {
        // Only separators -> RemoveEmptyEntries yields zero entries.
        var (correct, matched, missed) = TestGrading.GradeFreeText("answer", " , ; \n ", 1);
        Assert.Null(correct);
        Assert.Empty(matched);
        Assert.Empty(missed);
    }

    // ---------- GradeFreeText: all / partial / none ----------

    [Fact]
    public void GradeFreeText_allKeywordsHit_isCorrect()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "alpha beta gamma", "alpha; beta; gamma", null);

        Assert.Equal(true, correct);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, matched);
        Assert.Empty(missed);
    }

    [Fact]
    public void GradeFreeText_partialHits_matchedAndMissedTrackedInOrder()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "alpha beta", "alpha; beta; gamma", null);

        // default minHits == entry count (3) so 2/3 is not enough
        Assert.Equal(false, correct);
        Assert.Equal(new[] { "alpha", "beta" }, matched);
        Assert.Equal(new[] { "gamma" }, missed);
        Assert.Equal(2, matched.Count);
        Assert.Single(missed);
    }

    [Fact]
    public void GradeFreeText_noHits_allMissedAndNotCorrect()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "nothing relevant here", "alpha; beta; gamma", null);

        Assert.Equal(false, correct);
        Assert.Empty(matched);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, missed);
    }

    [Fact]
    public void GradeFreeText_nullAnswer_allKeywordsMissed()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            null, "alpha; beta", null);

        Assert.Equal(false, correct);
        Assert.Empty(matched);
        Assert.Equal(new[] { "alpha", "beta" }, missed);
    }

    // ---------- GradeFreeText: minHits clamp ladder ----------

    [Theory]
    // 3 keywords, answer hits exactly 2 (alpha, beta). required = Clamp(minHits ?? 3, 1, 3).
    [InlineData(null, false)] // required 3 -> 2 >= 3 == false
    [InlineData(3, false)]    // required 3
    [InlineData(10, false)]   // clamped to 3
    [InlineData(2, true)]     // required 2 -> 2 >= 2
    [InlineData(1, true)]     // required 1
    [InlineData(0, true)]     // clamped up to 1
    [InlineData(-5, true)]    // clamped up to 1
    public void GradeFreeText_minHitsClamp_decidesCorrectness(int? minHits, bool expectedCorrect)
    {
        var (correct, matched, _) = TestGrading.GradeFreeText(
            "alpha beta", "alpha; beta; gamma", minHits);

        Assert.Equal(2, matched.Count);
        Assert.Equal(expectedCorrect, correct);
    }

    // ---------- GradeFreeText: splitting & trimming ----------

    [Theory]
    [InlineData("alpha\nbeta\ngamma")]     // newlines
    [InlineData("alpha\r\nbeta\r\ngamma")] // CRLF
    [InlineData("alpha,beta,gamma")]        // commas
    [InlineData("alpha;beta;gamma")]        // semicolons
    [InlineData("  alpha ,  beta ; gamma ")] // padded -> TrimEntries
    [InlineData("alpha,,beta,;,gamma")]      // empty fragments removed
    public void GradeFreeText_variousDelimitersAndPadding_produceThreeKeywords(string keywords)
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "alpha beta gamma", keywords, null);

        Assert.Equal(true, correct);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, matched);
        Assert.Empty(missed);
    }

    // ---------- GradeFreeText: case handling ----------

    [Fact]
    public void GradeFreeText_matchingIsCaseInsensitive()
    {
        // uppercase keywords vs lowercase answer still match (tokens are lowercased)
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "alpha beta", "ALPHA; Beta", null);

        Assert.Equal(true, correct);
        Assert.Equal(new[] { "ALPHA", "Beta" }, matched); // original casing preserved in output
        Assert.Empty(missed);
    }

    [Fact]
    public void GradeFreeText_shortKeyword_usesExactTokenPresence()
    {
        // "id" is shorter than MinWordLength (3) -> exact token presence, not fuzzy
        var (matchCorrect, matched, _) = TestGrading.GradeFreeText(
            "my id card", "id", null);
        Assert.Equal(true, matchCorrect);
        Assert.Equal(new[] { "id" }, matched);

        // substring inside a longer word is NOT a token match
        var (noMatchCorrect, _, missed) = TestGrading.GradeFreeText(
            "identity document", "id", null);
        Assert.Equal(false, noMatchCorrect);
        Assert.Equal(new[] { "id" }, missed);
    }

    [Fact]
    public void GradeFreeText_shortKeyword_caseInsensitiveExactMatch()
    {
        var (correct, matched, _) = TestGrading.GradeFreeText(
            "my id here", "ID", null);
        Assert.Equal(true, correct);
        Assert.Equal(new[] { "ID" }, matched);
    }

    // ---------- GradeFreeText: fuzzy tolerance ----------

    [Fact]
    public void GradeFreeText_longKeyword_toleratesSingleCharTypoWithinThreshold()
    {
        // keyword "Loyalitaet" vs answer token "loyalitaeg" (1 edit) -> fuzzy hit
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "Er zeigt loyalitaeg gegenueber", "Loyalitaet", null);

        Assert.Equal(true, correct);
        Assert.Equal(new[] { "Loyalitaet" }, matched);
        Assert.Empty(missed);
    }

    [Fact]
    public void GradeFreeText_longKeyword_missesWhenBeyondThreshold()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "voellig anderes thema hier", "Loyalitaet", null);

        Assert.Equal(false, correct);
        Assert.Empty(matched);
        Assert.Equal(new[] { "Loyalitaet" }, missed);
    }

    // ---------- GradeFreeText: keyword with no alphanumeric tokens ----------

    [Fact]
    public void GradeFreeText_keywordWithoutAlnumTokens_isMissed()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "some real answer", "!!!", 1);

        Assert.Equal(false, correct);
        Assert.Empty(matched);
        Assert.Equal(new[] { "!!!" }, missed);
    }

    [Fact]
    public void GradeFreeText_singleKeywordHit_isCorrectWithDefaultMinHits()
    {
        var (correct, matched, missed) = TestGrading.GradeFreeText(
            "alpha", "alpha", null);

        Assert.Equal(true, correct);
        Assert.Equal(new[] { "alpha" }, matched);
        Assert.Empty(missed);
    }
}
