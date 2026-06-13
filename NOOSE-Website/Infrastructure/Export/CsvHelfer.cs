using System.Text;

namespace NOOSE_Website.Infrastructure.Export;

/// <summary>
/// Baut CSV-Dateien für den Statistik-Export. Bewusst auf das deutsche Excel zugeschnitten:
/// Trennzeichen <c>;</c> (DE-Excel-Standard) und ein vorangestelltes UTF-8-BOM, damit Excel die Datei
/// als UTF-8 erkennt und Umlaute (ä/ö/ü/ß) korrekt darstellt. Felder werden nach RFC 4180 maskiert.
/// </summary>
public static class CsvHelfer
{
    private const char Trenner = ';';

    /// <summary>
    /// Erzeugt die CSV-Bytes (UTF-8 mit BOM) aus einer Kopfzeile und beliebig vielen Datenzeilen.
    /// Jede Zeile ist eine Folge von Feldern; <c>null</c>-Felder werden als Leerstring geschrieben.
    /// </summary>
    public static byte[] Erzeuge(IEnumerable<string> kopf, IEnumerable<IEnumerable<string>> zeilen)
    {
        var sb = new StringBuilder();
        SchreibeZeile(sb, kopf);
        foreach (var zeile in zeilen)
        {
            SchreibeZeile(sb, zeile);
        }

        // GetBytes fügt selbst kein BOM hinzu – das stellen wir explizit voran (Preamble von UTF8Encoding(true)).
        var bom = new UTF8Encoding(true).GetPreamble();
        var inhalt = Encoding.UTF8.GetBytes(sb.ToString());
        var ergebnis = new byte[bom.Length + inhalt.Length];
        Buffer.BlockCopy(bom, 0, ergebnis, 0, bom.Length);
        Buffer.BlockCopy(inhalt, 0, ergebnis, bom.Length, inhalt.Length);
        return ergebnis;
    }

    private static void SchreibeZeile(StringBuilder sb, IEnumerable<string> felder)
    {
        var erstes = true;
        foreach (var feld in felder)
        {
            if (!erstes)
            {
                sb.Append(Trenner);
            }
            erstes = false;
            sb.Append(Maskiere(feld));
        }
        // Excel/CSV erwarten CRLF als Zeilenende.
        sb.Append("\r\n");
    }

    // RFC 4180: enthält ein Feld Trennzeichen, Anführungszeichen oder Zeilenumbruch, wird es in
    // doppelte Anführungszeichen gesetzt; enthaltene Anführungszeichen werden verdoppelt.
    private static string Maskiere(string? feld)
    {
        var s = feld ?? string.Empty;
        if (s.IndexOf(Trenner) >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
