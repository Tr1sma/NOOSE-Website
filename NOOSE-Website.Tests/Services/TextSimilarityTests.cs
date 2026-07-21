using NOOSE_Website.Services;
using Xunit;

namespace NOOSE_Website.Tests.Services;

public class TextSimilarityTests
{
    // ---- Threshold ----

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]   // boundary: <= 4 -> 1
    [InlineData(5, 2)]   // boundary: > 4 -> 2
    [InlineData(10, 2)]
    [InlineData(100, 2)]
    public void Threshold_maps_word_length_to_allowed_edits(int wordLength, int expected)
    {
        Assert.Equal(expected, TextSimilarity.Threshold(wordLength));
    }

    [Fact]
    public void MinWordLength_is_three()
    {
        Assert.Equal(3, TextSimilarity.MinWordLength);
    }

    // ---- Distance: trivial/empty cases ----

    [Fact]
    public void Distance_identical_strings_is_zero()
    {
        Assert.Equal(0, TextSimilarity.Distance("hello", "hello"));
    }

    [Fact]
    public void Distance_both_empty_is_zero()
    {
        Assert.Equal(0, TextSimilarity.Distance("", ""));
    }

    [Fact]
    public void Distance_empty_first_returns_length_of_second()
    {
        Assert.Equal(3, TextSimilarity.Distance("", "abc"));
    }

    [Fact]
    public void Distance_empty_second_returns_length_of_first()
    {
        Assert.Equal(4, TextSimilarity.Distance("abcd", ""));
    }

    // ---- Distance: single edits ----

    [Fact]
    public void Distance_single_substitution_is_one()
    {
        Assert.Equal(1, TextSimilarity.Distance("cat", "car"));
    }

    [Fact]
    public void Distance_single_insertion_is_one()
    {
        Assert.Equal(1, TextSimilarity.Distance("cat", "cart"));
    }

    [Fact]
    public void Distance_single_deletion_is_one()
    {
        Assert.Equal(1, TextSimilarity.Distance("cart", "cat"));
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("flaw", "lawn", 2)]
    [InlineData("book", "back", 2)]
    [InlineData("abc", "xyz", 3)]
    public void Distance_multi_edit_cases(string a, string b, int expected)
    {
        Assert.Equal(expected, TextSimilarity.Distance(a, b));
    }

    [Fact]
    public void Distance_transposition_counts_as_two_edits()
    {
        // Plain Levenshtein (not Damerau): a swap costs two substitutions.
        Assert.Equal(2, TextSimilarity.Distance("ab", "ba"));
        Assert.Equal(2, TextSimilarity.Distance("abc", "acb"));
    }

    // ---- Distance: symmetry ----

    [Theory]
    [InlineData("hello", "world")]
    [InlineData("kitten", "sitting")]
    [InlineData("", "abc")]
    [InlineData("cat", "cart")]
    [InlineData("abcdef", "uv")]
    [InlineData("same", "same")]
    public void Distance_is_symmetric(string a, string b)
    {
        Assert.Equal(TextSimilarity.Distance(a, b), TextSimilarity.Distance(b, a));
    }

    // ---- Distance: maxDistance early bail-out ----

    [Fact]
    public void Distance_when_within_bound_returns_true_distance()
    {
        Assert.Equal(1, TextSimilarity.Distance("cat", "car", 5));
    }

    [Fact]
    public void Distance_when_exceeding_bound_returns_max_plus_one()
    {
        // True distance is 6; capped search bails out as maxDistance + 1.
        Assert.Equal(3, TextSimilarity.Distance("abcdef", "uvwxyz", 2));
    }

    [Fact]
    public void Distance_identical_with_zero_bound_does_not_bail()
    {
        Assert.Equal(0, TextSimilarity.Distance("abc", "abc", 0));
    }

    [Fact]
    public void Distance_exact_bound_returns_true_distance()
    {
        // distance == maxDistance is not exceeding, so true value is returned.
        Assert.Equal(2, TextSimilarity.Distance("ab", "ba", 2));
    }

    // ---- Tokens ----

    [Fact]
    public void Tokens_splits_on_whitespace_lowercasing()
    {
        var result = TextSimilarity.Tokens("Hello World");
        Assert.Equal(2, result.Count);
        Assert.Contains("hello", result);
        Assert.Contains("world", result);
    }

    [Fact]
    public void Tokens_deduplicates_across_case_and_punctuation()
    {
        var result = TextSimilarity.Tokens("Hello, hello! HELLO");
        Assert.Single(result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public void Tokens_splits_on_non_alphanumeric_boundaries()
    {
        var result = TextSimilarity.Tokens("a-b_c.d");
        Assert.Equal(4, result.Count);
        Assert.Contains("a", result);
        Assert.Contains("b", result);
        Assert.Contains("c", result);
        Assert.Contains("d", result);
    }

    [Fact]
    public void Tokens_keeps_alphanumeric_words_intact()
    {
        var result = TextSimilarity.Tokens("abc123 X9");
        Assert.Equal(2, result.Count);
        Assert.Contains("abc123", result);
        Assert.Contains("x9", result);
    }

    [Fact]
    public void Tokens_null_input_returns_empty()
    {
        Assert.Empty(TextSimilarity.Tokens((string?)null));
    }

    [Fact]
    public void Tokens_empty_string_returns_empty()
    {
        Assert.Empty(TextSimilarity.Tokens(""));
    }

    [Fact]
    public void Tokens_whitespace_and_punctuation_only_returns_empty()
    {
        Assert.Empty(TextSimilarity.Tokens("  ,.-!  "));
    }

    [Fact]
    public void Tokens_no_arguments_returns_empty()
    {
        Assert.Empty(TextSimilarity.Tokens());
    }

    [Fact]
    public void Tokens_merges_multiple_texts_distinctly()
    {
        var result = TextSimilarity.Tokens("foo bar", "bar baz");
        Assert.Equal(3, result.Count);
        Assert.Contains("foo", result);
        Assert.Contains("bar", result);
        Assert.Contains("baz", result);
    }

    [Fact]
    public void Tokens_skips_null_and_empty_entries_among_texts()
    {
        var result = TextSimilarity.Tokens(null, "Solo", "", "   ");
        Assert.Single(result);
        Assert.Contains("solo", result);
    }

    [Fact]
    public void Tokens_flushes_trailing_word_at_end_of_text()
    {
        var result = TextSimilarity.Tokens("trailing");
        Assert.Single(result);
        Assert.Contains("trailing", result);
    }

    // ---- PhraseSimilar ----

    [Fact]
    public void PhraseSimilar_exact_match_returns_true_zero_distance()
    {
        var ok = TextSimilarity.PhraseSimilar(new[] { "hello" }, new[] { "hello" }, out var sum);
        Assert.True(ok);
        Assert.Equal(0, sum);
    }

    [Fact]
    public void PhraseSimilar_single_edit_within_threshold_matches()
    {
        // "hello" (len 5 -> threshold 2), "hallo" is distance 1.
        var ok = TextSimilarity.PhraseSimilar(new[] { "hello" }, new[] { "hallo" }, out var sum);
        Assert.True(ok);
        Assert.Equal(1, sum);
    }

    [Fact]
    public void PhraseSimilar_above_threshold_returns_false()
    {
        // distance("hello","world") == 4 > threshold 2.
        var ok = TextSimilarity.PhraseSimilar(new[] { "hello" }, new[] { "world" }, out var sum);
        Assert.False(ok);
        Assert.Equal(0, sum);
    }

    [Fact]
    public void PhraseSimilar_short_word_threshold_boundary_matches_at_one()
    {
        // "cat" (len 3 -> threshold 1), "car" is distance 1 (== threshold).
        var ok = TextSimilarity.PhraseSimilar(new[] { "cat" }, new[] { "car" }, out var sum);
        Assert.True(ok);
        Assert.Equal(1, sum);
    }

    [Fact]
    public void PhraseSimilar_short_word_over_threshold_returns_false()
    {
        // "cat" (threshold 1), "dog" is distance 3 > 1.
        var ok = TextSimilarity.PhraseSimilar(new[] { "cat" }, new[] { "dog" }, out _);
        Assert.False(ok);
    }

    [Fact]
    public void PhraseSimilar_no_candidates_returns_false()
    {
        var ok = TextSimilarity.PhraseSimilar(new[] { "hello" }, Array.Empty<string>(), out var sum);
        Assert.False(ok);
        Assert.Equal(0, sum);
    }

    [Fact]
    public void PhraseSimilar_length_gap_alone_exceeds_threshold_returns_false()
    {
        // "hello" (len 5, threshold 2) vs "hi" (len 2): gap 3 > 2, candidate skipped.
        var ok = TextSimilarity.PhraseSimilar(new[] { "hello" }, new[] { "hi" }, out _);
        Assert.False(ok);
    }

    [Fact]
    public void PhraseSimilar_all_search_words_too_short_returns_false()
    {
        // Both words shorter than MinWordLength (3) -> nothing checked.
        var ok = TextSimilarity.PhraseSimilar(new[] { "ab", "cd" }, new[] { "ab", "cd" }, out var sum);
        Assert.False(ok);
        Assert.Equal(0, sum);
    }

    [Fact]
    public void PhraseSimilar_skips_short_words_but_matches_long_ones()
    {
        // "ab" is skipped; "hello" matches exactly -> someChecked true.
        var ok = TextSimilarity.PhraseSimilar(new[] { "ab", "hello" }, new[] { "hello" }, out var sum);
        Assert.True(ok);
        Assert.Equal(0, sum);
    }

    [Fact]
    public void PhraseSimilar_all_words_must_match()
    {
        // "hello" matches, "zzzzz" has no near candidate -> overall false.
        var ok = TextSimilarity.PhraseSimilar(new[] { "hello", "zzzzz" }, new[] { "hello" }, out _);
        Assert.False(ok);
    }

    [Fact]
    public void PhraseSimilar_sums_best_distance_across_words()
    {
        // "hallo"->"hello" best distance 1; "world"->"world" best distance 0; sum 1.
        var ok = TextSimilarity.PhraseSimilar(
            new[] { "hallo", "world" },
            new[] { "hello", "world" },
            out var sum);
        Assert.True(ok);
        Assert.Equal(1, sum);
    }

    [Fact]
    public void PhraseSimilar_picks_closest_candidate_among_many()
    {
        // Best match for "hello" is the exact "hello", not "world".
        var ok = TextSimilarity.PhraseSimilar(
            new[] { "hello" },
            new[] { "world", "hxllo", "hello" },
            out var sum);
        Assert.True(ok);
        Assert.Equal(0, sum);
    }

    [Fact]
    public void PhraseSimilar_empty_search_words_returns_false()
    {
        var ok = TextSimilarity.PhraseSimilar(Array.Empty<string>(), new[] { "hello" }, out var sum);
        Assert.False(ok);
        Assert.Equal(0, sum);
    }
}
