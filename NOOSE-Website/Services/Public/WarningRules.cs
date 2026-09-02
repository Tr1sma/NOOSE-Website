namespace NOOSE_Website.Services.Public;

/// <summary>Limits of a public warning; read by the service, the panel and the public hub alike.</summary>
public static class WarningRules
{
    public const int MaxTitle = 200;

    /// <summary>How many warnings the hub shows; the page names the cap rather than cutting silently.</summary>
    /// <remarks>
    /// Lower than the press cap on purpose: every card carries its whole body, and a standing warning that nobody
    /// reads down to is a warning that did not work.
    /// </remarks>
    public const int HubLimit = 20;
}
