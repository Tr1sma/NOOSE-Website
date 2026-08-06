using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Which of the two NOOSE cash books a booking belongs to.</summary>
public enum KassenKonto
{
    Schwarzgeld = 0,
    Gruengeld = 1,
}

/// <summary>Display labels, icons and accent colors per cash account.</summary>
public static class KassenKontoDisplay
{
    public static string Name(KassenKonto account) => account switch
    {
        KassenKonto.Schwarzgeld => "Schwarzgeld",
        KassenKonto.Gruengeld => "Grüngeld",
        _ => "—",
    };

    public static string Icon(KassenKonto account) => account switch
    {
        KassenKonto.Schwarzgeld => Icons.Material.Filled.MoneyOff,
        KassenKonto.Gruengeld => Icons.Material.Filled.Payments,
        _ => Icons.Material.Filled.AccountBalanceWallet,
    };

    public static Color Colour(KassenKonto account) => account switch
    {
        KassenKonto.Schwarzgeld => Color.Error,
        KassenKonto.Gruengeld => Color.Success,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<KassenKonto> All = new[]
    {
        KassenKonto.Schwarzgeld,
        KassenKonto.Gruengeld,
    };
}
