using System.Text;

namespace NOOSE_Website.Infrastructure.Export;

/// <summary>
/// Baut CSV-Dateien für den Statistik-Export. Bewusst auf das deutsche Excel zugeschnitten:
/// Trennzeichen <c>;</c> (DE-Excel-Standard) und ein vorangestelltes UTF-8-BOM, damit Excel die Datei
/// als UTF-8 erkennt und Umlaute (ä/ö/ü/ß) korrekt darstellt. Felder werden nach RFC 4180 maskiert.
/// </summary>
public static class CsvHelper
{
    private const char Separator = ';';

    /// <summary>
    /// Erzeugt die CSV-Bytes (UTF-8 mit BOM) aus einer Kopfzeile und beliebig vielen Datenzeilen.
    /// Jede Zeile ist eine Folge von Feldern; <c>null</c>-Felder werden als Leerstring geschrieben.
    /// </summary>
    public static byte[] Generate(IEnumerable<string> head, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        WriteRow(sb, head);
        foreach (var row in rows)
        {
            WriteRow(sb, row);
        }

        // GetBytes fügt selbst kein BOM hinzu – das stellen wir explizit voran (Preamble von UTF8Encoding(true)).
        var bom = new UTF8Encoding(true).GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + content.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(content, 0, result, bom.Length, content.Length);
        return result;
    }

    private static void WriteRow(StringBuilder sb, IEnumerable<string> fields)
    {
        var first = true;
        foreach (var field in fields)
        {
            if (!first)
            {
                sb.Append(Separator);
            }
            first = false;
            sb.Append(Mask(field));
        }
        // Excel/CSV erwarten CRLF als Zeilenende.
        sb.Append("\r\n");
    }

    // RFC 4180: enthält ein Feld Trennzeichen, Anführungszeichen oder Zeilenumbruch, wird es in
    // doppelte Anführungszeichen gesetzt; enthaltene Anführungszeichen werden verdoppelt.
    private static string Mask(string? field)
    {
        var s = field ?? string.Empty;
        if (s.IndexOf(Separator) >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
