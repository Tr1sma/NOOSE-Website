using System.Text;

namespace NOOSE_Website.Services;

/// <summary>
/// Wortbasierte Ähnlichkeits-Hilfen (Levenshtein) für die globale Suche. Reine CPU-Logik ohne
/// Datenbank/UI – arbeitet auf bereits geladenen Zeichenketten. MySQL/MariaDB kennt keine
/// Editierdistanz und Pomelo übersetzt sie nicht, daher läuft die Tippfehler-Toleranz in-memory
/// über diesen Helfer (als Ergänzung zur exakten LIKE-Suche, nicht als Ersatz).
/// </summary>
public static class TextAehnlichkeit
{
    /// <summary>Suchwörter kürzer als dies lösen keine Fuzzy-Treffer aus (sonst zu viel Rauschen).</summary>
    public const int MinWortLaenge = 3;

    /// <summary>Erlaubte Editierdistanz je Wortlänge: kurze Wörter strenger, längere etwas toleranter.</summary>
    public static int Schwelle(int wortLaenge) => wortLaenge <= 4 ? 1 : 2;

    /// <summary>
    /// Zerlegt beliebige Texte in eindeutige, kleingeschriebene Wörter (Trennung an allem, was kein
    /// Buchstabe/keine Ziffer ist). Umlaute bleiben erhalten. Null/Leeres wird übersprungen.
    /// </summary>
    public static IReadOnlyList<string> Tokens(params string?[] texte)
    {
        var menge = new HashSet<string>(StringComparer.Ordinal);
        var wort = new StringBuilder();
        foreach (var text in texte)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    wort.Append(char.ToLowerInvariant(c));
                }
                else if (wort.Length > 0)
                {
                    menge.Add(wort.ToString());
                    wort.Clear();
                }
            }
            if (wort.Length > 0)
            {
                menge.Add(wort.ToString());
                wort.Clear();
            }
        }
        return menge.Count == 0 ? Array.Empty<string>() : menge.ToList();
    }

    /// <summary>
    /// Prüft, ob ein Kandidat zum Suchbegriff „ähnlich genug" ist: JEDES Suchwort (ab
    /// <see cref="MinWortLaenge"/> Zeichen) braucht ein Kandidatenwort innerhalb seiner Schwelle.
    /// <paramref name="summeDistanz"/> liefert die aufsummierte Mindestdistanz für die Sortierung
    /// (kleiner = relevanter). Gibt false zurück, wenn kein Suchwort lang genug zum Prüfen ist.
    /// </summary>
    public static bool PhraseAehnlich(IReadOnlyList<string> suchworte, IReadOnlyList<string> kandidatworte, out int summeDistanz)
    {
        summeDistanz = 0;
        var etwasGeprueft = false;
        foreach (var suchwort in suchworte)
        {
            if (suchwort.Length < MinWortLaenge)
            {
                continue;
            }
            etwasGeprueft = true;
            var schwelle = Schwelle(suchwort.Length);
            var besteDistanz = int.MaxValue;
            foreach (var kandidat in kandidatworte)
            {
                if (Math.Abs(kandidat.Length - suchwort.Length) > schwelle)
                {
                    continue; // Längenunterschied allein überschreitet schon die Schwelle.
                }
                var d = Distanz(suchwort, kandidat, schwelle);
                if (d < besteDistanz)
                {
                    besteDistanz = d;
                    if (besteDistanz == 0)
                    {
                        break;
                    }
                }
            }
            if (besteDistanz > schwelle)
            {
                return false; // Dieses Suchwort hat kein hinreichend ähnliches Wort → kein Treffer.
            }
            summeDistanz += besteDistanz;
        }
        return etwasGeprueft;
    }

    /// <summary>
    /// Levenshtein-Distanz mit oberer Schranke <paramref name="maxDistanz"/>: Sobald eine ganze
    /// Matrix-Zeile die Schranke überschreitet, wird vorzeitig mit <c>maxDistanz + 1</c> abgebrochen.
    /// Zwei rollende Zeilen statt voller Matrix (Speicher O(n)).
    /// </summary>
    public static int Distanz(string a, string b, int maxDistanz = int.MaxValue)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }
        if (b.Length == 0)
        {
            return a.Length;
        }

        var vorherige = new int[b.Length + 1];
        var aktuelle = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            vorherige[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            aktuelle[0] = i;
            var zeilenMin = aktuelle[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var kosten = a[i - 1] == b[j - 1] ? 0 : 1;
                aktuelle[j] = Math.Min(Math.Min(aktuelle[j - 1] + 1, vorherige[j] + 1), vorherige[j - 1] + kosten);
                if (aktuelle[j] < zeilenMin)
                {
                    zeilenMin = aktuelle[j];
                }
            }
            if (zeilenMin > maxDistanz)
            {
                return maxDistanz + 1;
            }
            (vorherige, aktuelle) = (aktuelle, vorherige);
        }
        return vorherige[b.Length];
    }
}
