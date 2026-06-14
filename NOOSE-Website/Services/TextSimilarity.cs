using System.Text;

namespace NOOSE_Website.Services;

/// <summary>
/// Wortbasierte Ähnlichkeits-Hilfen (Levenshtein) für die globale Suche. Reine CPU-Logik ohne
/// Datenbank/UI – arbeitet auf bereits geladenen Zeichenketten. MySQL/MariaDB kennt keine
/// Editierdistanz und Pomelo übersetzt sie nicht, daher läuft die Tippfehler-Toleranz in-memory
/// über diesen Helfer (als Ergänzung zur exakten LIKE-Suche, nicht als Ersatz).
/// </summary>
public static class TextSimilarity
{
    /// <summary>Suchwörter kürzer als dies lösen keine Fuzzy-Treffer aus (sonst zu viel Rauschen).</summary>
    public const int MinWordLength = 3;

    /// <summary>Erlaubte Editierdistanz je Wortlänge: kurze Wörter strenger, längere etwas toleranter.</summary>
    public static int Threshold(int wordLength) => wordLength <= 4 ? 1 : 2;

    /// <summary>
    /// Zerlegt beliebige Texte in eindeutige, kleingeschriebene Wörter (Trennung an allem, was kein
    /// Buchstabe/keine Ziffer ist). Umlaute bleiben erhalten. Null/Leeres wird übersprungen.
    /// </summary>
    public static IReadOnlyList<string> Tokens(params string?[] texts)
    {
        var quantity = new HashSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else if (word.Length > 0)
                {
                    quantity.Add(word.ToString());
                    word.Clear();
                }
            }
            if (word.Length > 0)
            {
                quantity.Add(word.ToString());
                word.Clear();
            }
        }
        return quantity.Count == 0 ? Array.Empty<string>() : quantity.ToList();
    }

    /// <summary>
    /// Prüft, ob ein Kandidat zum Suchbegriff „ähnlich genug" ist: JEDES Suchwort (ab
    /// <see cref="MinWortLaenge"/> Zeichen) braucht ein Kandidatenwort innerhalb seiner Schwelle.
    /// <paramref name="summeDistanz"/> liefert die aufsummierte Mindestdistanz für die Sortierung
    /// (kleiner = relevanter). Gibt false zurück, wenn kein Suchwort lang genug zum Prüfen ist.
    /// </summary>
    public static bool PhraseSimilar(IReadOnlyList<string> searchWords, IReadOnlyList<string> candidateWords, out int sumDistance)
    {
        sumDistance = 0;
        var someChecked = false;
        foreach (var searchWord in searchWords)
        {
            if (searchWord.Length < MinWordLength)
            {
                continue;
            }
            someChecked = true;
            var threshold = Threshold(searchWord.Length);
            var bestDistance = int.MaxValue;
            foreach (var candidate in candidateWords)
            {
                if (Math.Abs(candidate.Length - searchWord.Length) > threshold)
                {
                    continue; // Längenunterschied allein überschreitet schon die Schwelle.
                }
                var d = Distance(searchWord, candidate, threshold);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    if (bestDistance == 0)
                    {
                        break;
                    }
                }
            }
            if (bestDistance > threshold)
            {
                return false; // Dieses Suchwort hat kein hinreichend ähnliches Wort → kein Treffer.
            }
            sumDistance += bestDistance;
        }
        return someChecked;
    }

    /// <summary>
    /// Levenshtein-Distanz mit oberer Schranke <paramref name="maxDistanz"/>: Sobald eine ganze
    /// Matrix-Zeile die Schranke überschreitet, wird vorzeitig mit <c>maxDistanz + 1</c> abgebrochen.
    /// Zwei rollende Zeilen statt voller Matrix (Speicher O(n)).
    /// </summary>
    public static int Distance(string a, string b, int maxDistance = int.MaxValue)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }
        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowsMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (current[j] < rowsMin)
                {
                    rowsMin = current[j];
                }
            }
            if (rowsMin > maxDistance)
            {
                return maxDistance + 1;
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
