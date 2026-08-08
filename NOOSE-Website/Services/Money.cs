using System.Globalization;

namespace NOOSE_Website.Services;

/// <summary>Formats in-game cash amounts (GTA dollars) with German grouping and a trailing symbol.</summary>
public static class Money
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>e.g. 46000028 → "46.000.028 $".</summary>
    public static string Format(decimal amount) => amount.ToString("N0", De) + " $";

    /// <summary>Signed ledger delta, e.g. "+279.888 $" / "−5.000 $".</summary>
    public static string Signed(decimal delta)
    {
        var sign = delta < 0 ? "−" : "+";
        return sign + Math.Abs(delta).ToString("N0", De) + " $";
    }
}
