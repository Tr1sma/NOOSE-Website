namespace NOOSE_Website.Services.Public;

/// <summary>Limits of a public situation report; read by the service, the panel and the public hub alike.</summary>
public static class ReportRules
{
    public const int MaxTitle = 200;

    /// <summary>How many reports the hub shows; the page names the cap rather than cutting silently.</summary>
    /// <remarks>Two years of monthly reports, which is as far back as a reader plausibly scrolls.</remarks>
    public const int HubLimit = 24;
}
