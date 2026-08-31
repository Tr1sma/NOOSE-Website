namespace NOOSE_Website.Services.Public;

/// <summary>Limits of a press release; read by the service, the panel and the public hub alike.</summary>
public static class PressRules
{
    public const int MaxTitle = 200;

    /// <summary>Plain text, so the hub can list it without rendering markup.</summary>
    public const int MaxTeaser = 400;

    /// <summary>How many releases the hub shows; the page names the cap rather than cutting silently.</summary>
    public const int HubLimit = 50;
}
