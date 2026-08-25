namespace NOOSE_Website.Models.Enums;

/// <summary>Lifecycle state of a public organisation profile; the only thing that decides public visibility.</summary>
/// <remarks>
/// Its own axis next to <see cref="PublicFactionStanding"/>: one column for both could not take a publication offline
/// without losing the label it was published with.
/// </remarks>
public enum PublicProfileStatus
{
    Entwurf = 0,
    Veroeffentlicht = 1,
    Zurueckgezogen = 2,
}

/// <summary>Display labels.</summary>
public static class PublicProfileStatusDisplay
{
    public static string Name(PublicProfileStatus status) => status switch
    {
        PublicProfileStatus.Entwurf => "Entwurf",
        PublicProfileStatus.Veroeffentlicht => "Veröffentlicht",
        PublicProfileStatus.Zurueckgezogen => "Zurückgezogen",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicProfileStatus> All = new[]
    {
        PublicProfileStatus.Entwurf,
        PublicProfileStatus.Veroeffentlicht,
        PublicProfileStatus.Zurueckgezogen,
    };
}
