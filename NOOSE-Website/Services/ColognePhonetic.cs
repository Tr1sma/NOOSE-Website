using System.Text;

namespace NOOSE_Website.Services;

/// <summary>Kölner Phonetik (Cologne phonetic) encoder: maps a German word to a digit code so that
/// similar-sounding names (Maier/Meyer/Mayr/Meier) share one key. Pure, DB-free.</summary>
public static class ColognePhonetic
{
    /// <summary>Encode a single word to its Cologne phonetic code (empty for empty/codeless input).</summary>
    public static string Encode(string? input)
    {
        var s = Normalize(input);
        if (s.Length == 0)
        {
            return string.Empty;
        }

        var digits = new StringBuilder(s.Length + 1);
        for (var i = 0; i < s.Length; i++)
        {
            var prev = i > 0 ? s[i - 1] : (char?)null;
            var next = i < s.Length - 1 ? s[i + 1] : (char?)null;
            digits.Append(CodeFor(s[i], prev, next, i == 0));
        }

        // collapse consecutive identical digits
        var collapsed = new StringBuilder(digits.Length);
        var last = '\0';
        foreach (var d in digits.ToString())
        {
            if (d != last)
            {
                collapsed.Append(d);
            }
            last = d;
        }

        // drop every '0' except a leading one
        var result = new StringBuilder(collapsed.Length);
        for (var i = 0; i < collapsed.Length; i++)
        {
            if (collapsed[i] == '0' && i != 0)
            {
                continue;
            }
            result.Append(collapsed[i]);
        }
        return result.ToString();
    }

    // uppercase A–Z only, umlauts folded (Ä→A, Ö→O, Ü→U, ß→SS)
    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }
        var sb = new StringBuilder(input.Length);
        foreach (var raw in input.Trim().ToUpperInvariant())
        {
            switch (raw)
            {
                case 'Ä': sb.Append('A'); break;
                case 'Ö': sb.Append('O'); break;
                case 'Ü': sb.Append('U'); break;
                case 'ß': sb.Append("SS"); break;
                default:
                    if (raw is >= 'A' and <= 'Z')
                    {
                        sb.Append(raw);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    // returns "" for codeless letters (H), "0" for vowels, else the consonant code(s)
    private static string CodeFor(char c, char? prev, char? next, bool first) => c switch
    {
        'A' or 'E' or 'I' or 'J' or 'O' or 'U' or 'Y' => "0",
        'H' => string.Empty,
        'B' => "1",
        'P' => next == 'H' ? "3" : "1",
        'D' or 'T' => next is 'C' or 'S' or 'Z' ? "8" : "2",
        'F' or 'V' or 'W' => "3",
        'G' or 'K' or 'Q' => "4",
        'L' => "5",
        'M' or 'N' => "6",
        'R' => "7",
        'S' or 'Z' => "8",
        'C' => CodeForC(prev, next, first),
        'X' => prev is 'C' or 'K' or 'Q' ? "8" : "48",
        _ => string.Empty,
    };

    private static string CodeForC(char? prev, char? next, bool first)
    {
        if (first)
        {
            return next is 'A' or 'H' or 'K' or 'L' or 'O' or 'Q' or 'R' or 'U' or 'X' ? "4" : "8";
        }
        if (prev is 'S' or 'Z')
        {
            return "8";
        }
        return next is 'A' or 'H' or 'K' or 'O' or 'Q' or 'U' or 'X' ? "4" : "8";
    }
}
