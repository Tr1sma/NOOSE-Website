using System.Text;

namespace NOOSE_Website.Infrastructure.Export;

/// <summary>Builds CSV files with a UTF-8 BOM and semicolon separator for German Excel; fields masked per RFC 4180.</summary>
public static class CsvHelper
{
    private const char Separator = ';';

    /// <summary>Generates UTF-8 BOM CSV bytes from a header row and data rows; null fields become empty strings.</summary>
    public static byte[] Generate(IEnumerable<string> head, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        WriteRow(sb, head);
        foreach (var row in rows)
        {
            WriteRow(sb, row);
        }

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
        sb.Append("\r\n");
    }

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
