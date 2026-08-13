namespace NOOSE_Website.Models.Enums;

/// <summary>Editorial state of a public CMS page.</summary>
public enum PublicPageStatus
{
    Entwurf = 0,
    Veroeffentlicht = 1,
}

/// <summary>Display labels.</summary>
public static class PublicPageStatusDisplay
{
    public static string Name(PublicPageStatus status) => status switch
    {
        PublicPageStatus.Entwurf => "Entwurf",
        PublicPageStatus.Veroeffentlicht => "Veröffentlicht",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicPageStatus> All = new[]
    {
        PublicPageStatus.Entwurf,
        PublicPageStatus.Veroeffentlicht,
    };
}
