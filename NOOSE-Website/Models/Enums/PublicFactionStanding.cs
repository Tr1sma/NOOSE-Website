namespace NOOSE_Website.Models.Enums;

/// <summary>How the agency publicly classifies an organisation.</summary>
/// <remarks>
/// An editorial label, deliberately not derived from <c>Faction.Classification</c>: that axis is the internal
/// assessment and putting it outside would publish which files the agency is working on.
/// </remarks>
public enum PublicFactionStanding
{
    Beobachtet = 0,
    Verboten = 1,
}

/// <summary>Display labels.</summary>
public static class PublicFactionStandingDisplay
{
    public static string Name(PublicFactionStanding standing) => standing switch
    {
        PublicFactionStanding.Beobachtet => "Beobachtet",
        PublicFactionStanding.Verboten => "Verboten",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicFactionStanding> All = new[]
    {
        PublicFactionStanding.Beobachtet,
        PublicFactionStanding.Verboten,
    };
}
