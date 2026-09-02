namespace NOOSE_Website.Models.Enums;

/// <summary>Editorial state of a press release.</summary>
/// <remarks>
/// Its own enum rather than sharing PublicPageStatus: one enum across two tables ties their migrations together, and
/// a third value wanted by one of them would arrive at the other unasked.
/// </remarks>
public enum PressReleaseStatus
{
    Entwurf = 0,
    Veroeffentlicht = 1,
}

/// <summary>Display labels.</summary>
public static class PressReleaseStatusDisplay
{
    public static string Name(PressReleaseStatus status) => status switch
    {
        PressReleaseStatus.Entwurf => "Entwurf",
        PressReleaseStatus.Veroeffentlicht => "Veröffentlicht",
        _ => "—",
    };

    public static readonly IReadOnlyList<PressReleaseStatus> All = new[]
    {
        PressReleaseStatus.Entwurf,
        PressReleaseStatus.Veroeffentlicht,
    };
}
