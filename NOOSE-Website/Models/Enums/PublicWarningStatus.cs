namespace NOOSE_Website.Models.Enums;

/// <summary>Editorial state of a public warning.</summary>
/// <remarks>
/// Its own enum rather than sharing PressReleaseStatus: one enum across two tables ties their migrations together, and
/// a third value wanted by one of them would arrive at the other unasked.
/// </remarks>
public enum PublicWarningStatus
{
    Entwurf = 0,
    Veroeffentlicht = 1,
}

/// <summary>Display labels.</summary>
public static class PublicWarningStatusDisplay
{
    public static string Name(PublicWarningStatus status) => status switch
    {
        PublicWarningStatus.Entwurf => "Entwurf",
        PublicWarningStatus.Veroeffentlicht => "Veröffentlicht",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicWarningStatus> All = new[]
    {
        PublicWarningStatus.Entwurf,
        PublicWarningStatus.Veroeffentlicht,
    };
}
