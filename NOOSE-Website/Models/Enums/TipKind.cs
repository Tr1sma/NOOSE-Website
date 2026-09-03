using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>What a citizen submission claims: an observation, or that the person is already caught.</summary>
/// <remarks>
/// A capture report shares the tips table because it shares everything that matters — the case number, the two-way
/// message thread, the inbox, the trust tier and the reward payout. Only the limits and the gates differ, and those
/// read this value.
/// </remarks>
public enum TipKind
{
    Beobachtung = 0,
    Ergreifung = 1,
}

/// <summary>Where the caught person is; a second axis, only meaningful on a capture report.</summary>
/// <remarks>
/// Its own enum rather than two more <see cref="TipKind"/> values: the kind decides which rules apply, this decides
/// how urgent the handover is. Folded together, every "is this a capture report" check would have to list two values.
/// </remarks>
public enum TipHandover
{
    Festgehalten = 0,
    Uebergeben = 1,
}

/// <summary>Display labels and icons.</summary>
public static class TipKindDisplay
{
    public static string Name(TipKind kind) => kind switch
    {
        TipKind.Beobachtung => "Beobachtung",
        TipKind.Ergreifung => "Ergreifungsmeldung",
        _ => "—",
    };

    public static string Icon(TipKind kind) => kind switch
    {
        TipKind.Beobachtung => Icons.Material.Filled.TipsAndUpdates,
        TipKind.Ergreifung => Icons.Material.Filled.LocalPolice,
        _ => Icons.Material.Filled.HelpOutline,
    };

    public static readonly IReadOnlyList<TipKind> All = new[]
    {
        TipKind.Beobachtung,
        TipKind.Ergreifung,
    };
}

/// <summary>Display labels and icons.</summary>
public static class TipHandoverDisplay
{
    public static string Name(TipHandover handover) => handover switch
    {
        TipHandover.Festgehalten => "Person wird festgehalten",
        TipHandover.Uebergeben => "Person bereits übergeben",
        _ => "—",
    };

    /// <summary>Chip form, next to the case number in the inbox.</summary>
    public static string ShortName(TipHandover handover) => handover switch
    {
        TipHandover.Festgehalten => "hält Person fest",
        TipHandover.Uebergeben => "übergeben",
        _ => "—",
    };

    /// <summary>Label of the location field; it asks a different question per state.</summary>
    public static string LocationLabel(TipHandover handover) => handover switch
    {
        TipHandover.Festgehalten => "Wo hältst du die Person fest?",
        TipHandover.Uebergeben => "Wo oder wem hast du die Person übergeben?",
        _ => "Ort",
    };

    public static string Icon(TipHandover handover) => handover switch
    {
        TipHandover.Festgehalten => Icons.Material.Filled.PanTool,
        TipHandover.Uebergeben => Icons.Material.Filled.HowToReg,
        _ => Icons.Material.Filled.HelpOutline,
    };

    public static readonly IReadOnlyList<TipHandover> All = new[]
    {
        TipHandover.Festgehalten,
        TipHandover.Uebergeben,
    };
}
