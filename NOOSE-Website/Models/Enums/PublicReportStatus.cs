namespace NOOSE_Website.Models.Enums;

/// <summary>Editorial state of a public situation report.</summary>
/// <remarks>
/// Its own enum rather than sharing PressReleaseStatus or PublicWarningStatus: one enum across several tables ties
/// their migrations together, and a third value wanted by one of them would arrive at the others unasked.
/// </remarks>
public enum PublicReportStatus
{
    Entwurf = 0,
    Veroeffentlicht = 1,
}

/// <summary>Display labels.</summary>
public static class PublicReportStatusDisplay
{
    public static string Name(PublicReportStatus status) => status switch
    {
        PublicReportStatus.Entwurf => "Entwurf",
        PublicReportStatus.Veroeffentlicht => "Veröffentlicht",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicReportStatus> All = new[]
    {
        PublicReportStatus.Entwurf,
        PublicReportStatus.Veroeffentlicht,
    };
}
