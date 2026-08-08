using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Whether a booking adds to, subtracts from, or sets the account balance.</summary>
public enum KassenBuchungArt
{
    Einzahlung = 0,
    Auszahlung = 1,
    Korrektur = 2,
}

/// <summary>Display labels, icons, chip colors and the arithmetic sign per booking kind.</summary>
public static class KassenBuchungArtDisplay
{
    public static string Name(KassenBuchungArt kind) => kind switch
    {
        KassenBuchungArt.Einzahlung => "Einzahlung",
        KassenBuchungArt.Auszahlung => "Auszahlung",
        KassenBuchungArt.Korrektur => "Stand setzen",
        _ => "—",
    };

    public static string Icon(KassenBuchungArt kind) => kind switch
    {
        KassenBuchungArt.Einzahlung => Icons.Material.Filled.TrendingUp,
        KassenBuchungArt.Auszahlung => Icons.Material.Filled.TrendingDown,
        KassenBuchungArt.Korrektur => Icons.Material.Filled.Tune,
        _ => Icons.Material.Filled.SwapVert,
    };

    public static Color ChipColor(KassenBuchungArt kind) => kind switch
    {
        KassenBuchungArt.Einzahlung => Color.Success,
        KassenBuchungArt.Auszahlung => Color.Warning,
        KassenBuchungArt.Korrektur => Color.Info,
        _ => Color.Default,
    };

    /// <summary>"+" deposit, "−" withdrawal, "=" correction.</summary>
    public static string Sign(KassenBuchungArt kind) => kind switch
    {
        KassenBuchungArt.Einzahlung => "+",
        KassenBuchungArt.Auszahlung => "−",
        KassenBuchungArt.Korrektur => "=",
        _ => string.Empty,
    };

    public static readonly IReadOnlyList<KassenBuchungArt> All = new[]
    {
        KassenBuchungArt.Einzahlung,
        KassenBuchungArt.Auszahlung,
        KassenBuchungArt.Korrektur,
    };
}
