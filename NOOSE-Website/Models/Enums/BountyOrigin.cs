using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Where the money on a head comes from.</summary>
/// <remarks>Internal only: the outside sees one total, never who paid which part of it.</remarks>
public enum BountyOrigin
{
    NooseKasse = 0,
    AgentPrivat = 1,
}

/// <summary>Display labels, icons and chip colours per bounty origin.</summary>
public static class BountyOriginDisplay
{
    public static string Name(BountyOrigin origin) => origin switch
    {
        BountyOrigin.NooseKasse => "Behördlich",
        BountyOrigin.AgentPrivat => "Privat",
        _ => "—",
    };

    public static string Icon(BountyOrigin origin) => origin switch
    {
        BountyOrigin.NooseKasse => Icons.Material.Filled.AccountBalance,
        BountyOrigin.AgentPrivat => Icons.Material.Filled.VolunteerActivism,
        _ => Icons.Material.Filled.Paid,
    };

    public static Color ChipColor(BountyOrigin origin) => origin switch
    {
        BountyOrigin.NooseKasse => Color.Primary,
        BountyOrigin.AgentPrivat => Color.Secondary,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<BountyOrigin> All = new[]
    {
        BountyOrigin.NooseKasse,
        BountyOrigin.AgentPrivat,
    };
}
