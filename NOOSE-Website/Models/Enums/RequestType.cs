namespace NOOSE_Website.Models.Enums;

/// <summary>Inbox request type.</summary>
public enum RequestType
{
    /// <summary>Classification upgrade request.</summary>
    Upgrade = 0,
}

/// <summary>Display labels.</summary>
public static class RequestTypeDisplay
{
    public static string Name(RequestType type) => type switch
    {
        RequestType.Upgrade => "Hochstufung",
        _ => "—",
    };
}
