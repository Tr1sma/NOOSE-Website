namespace NOOSE_Website.Services;

/// <summary>Builds the persisted search side-index keys: phonetic codes (from name fields) and word stems
/// (from all searchable text). Reuses <see cref="TextSimilarity.Tokens"/> for splitting/normalisation. Pure, DB-free.</summary>
public static class SearchTokenizer
{
    // must match the side-index column sizes (Suche_WortStaemme.Stamm / Suche_PhonetikSchluessel.Schluessel);
    // over-length tokens are truncated so an unbroken 65+ char run cannot overflow the column and abort the save
    public const int MaxStemLength = 64;
    public const int MaxPhoneticLength = 32;

    /// <summary>Distinct word stems across all given texts (for the stem side-index).</summary>
    public static IReadOnlyList<string> Stems(params string?[] texts)
        => TextSimilarity.Tokens(texts)
            .Select(GermanStemmer.Stem)
            .Where(s => s.Length > 0)
            .Select(s => s.Length > MaxStemLength ? s[..MaxStemLength] : s)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Distinct Cologne phonetic codes across the given name fields (for the phonetic side-index).</summary>
    public static IReadOnlyList<string> PhoneticKeys(params string?[] names)
        => TextSimilarity.Tokens(names)
            .Select(ColognePhonetic.Encode)
            .Where(k => k.Length > 0)
            .Select(k => k.Length > MaxPhoneticLength ? k[..MaxPhoneticLength] : k)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
