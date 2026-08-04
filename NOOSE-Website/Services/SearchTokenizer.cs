namespace NOOSE_Website.Services;

/// <summary>Builds the persisted search side-index keys: phonetic codes (from name fields) and word stems
/// (from all searchable text). Reuses <see cref="TextSimilarity.Tokens"/> for splitting/normalisation. Pure, DB-free.</summary>
public static class SearchTokenizer
{
    /// <summary>Distinct word stems across all given texts (for the stem side-index).</summary>
    public static IReadOnlyList<string> Stems(params string?[] texts)
        => TextSimilarity.Tokens(texts)
            .Select(GermanStemmer.Stem)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Distinct Cologne phonetic codes across the given name fields (for the phonetic side-index).</summary>
    public static IReadOnlyList<string> PhoneticKeys(params string?[] names)
        => TextSimilarity.Tokens(names)
            .Select(ColognePhonetic.Encode)
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
