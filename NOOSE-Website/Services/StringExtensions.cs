namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame String-Helfer der Service-Schicht. Zentralisiert die Eingabe-Normalisierung, die zuvor
/// als privates <c>Leer(...)</c> in mehreren Diensten kopiert war (eine Quelle der Wahrheit).
/// </summary>
public static class StringExtensions
{
    /// <summary>Trimmt den Wert; gibt <c>null</c> zurück, wenn er leer oder nur Whitespace ist.</summary>
    public static string? TrimToNull(this string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
