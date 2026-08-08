using System.Text;

namespace NOOSE_Website.Services;

/// <summary>Lightweight German suffix stemmer (Snowball-inspired): folds umlauts, then strips inflection
/// and common derivation endings so „Verhaftung"/„Verhaftungen" collapse to one stem. Pure, DB-free.</summary>
public static class GermanStemmer
{
    // inflection endings first (order = longest first so we strip the biggest match)
    private static readonly string[] InflectionEndings = { "ern", "em", "er", "en", "es", "e" };

    // derivation endings, longest first
    private static readonly string[] DerivationEndings = { "igkeit", "lichkeit", "ung", "heit", "keit", "isch", "lich", "end", "ig" };

    public static string Stem(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }
        var w = Fold(word.Trim().ToLowerInvariant());
        if (w.Length <= 3)
        {
            return w;
        }

        w = StripLongest(w, InflectionEndings, minRemaining: 3);
        w = StripS(w);
        w = StripLongest(w, DerivationEndings, minRemaining: 4);
        return w;
    }

    // fold German diacritics so a diacritic typo/variant still stems alike
    public static string Fold(string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return string.Empty;
        }
        var sb = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            switch (c)
            {
                case 'ä': sb.Append('a'); break;
                case 'ö': sb.Append('o'); break;
                case 'ü': sb.Append('u'); break;
                case 'ß': sb.Append("ss"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static string StripLongest(string w, string[] endings, int minRemaining)
    {
        foreach (var suffix in endings)
        {
            if (w.Length - suffix.Length >= minRemaining && w.EndsWith(suffix, StringComparison.Ordinal))
            {
                return w[..^suffix.Length];
            }
        }
        return w;
    }

    // trailing -s only after a vowel or a common s-plural consonant (avoids butchering short stems)
    private static string StripS(string w)
    {
        if (w.Length >= 4 && w.EndsWith('s'))
        {
            var before = w[^2];
            if (before is 'a' or 'e' or 'i' or 'o' or 'u' or 'n' or 'r' or 't')
            {
                return w[..^1];
            }
        }
        return w;
    }
}
