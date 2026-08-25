namespace NOOSE_Website.Models.Enums;

/// <summary>Lifecycle state of a public wanted notice; the only thing that decides public visibility.</summary>
public enum PublicWantedStatus
{
    Entwurf = 0,
    Beantragt = 1,
    Veroeffentlicht = 2,
    Gefasst = 3,
    Zurueckgezogen = 4,
    Abgelaufen = 5,
}

/// <summary>Display labels.</summary>
public static class PublicWantedStatusDisplay
{
    public static string Name(PublicWantedStatus status) => status switch
    {
        PublicWantedStatus.Entwurf => "Entwurf",
        PublicWantedStatus.Beantragt => "Beantragt",
        PublicWantedStatus.Veroeffentlicht => "Veröffentlicht",
        PublicWantedStatus.Gefasst => "Gefasst",
        PublicWantedStatus.Zurueckgezogen => "Zurückgezogen",
        PublicWantedStatus.Abgelaufen => "Abgelaufen",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicWantedStatus> All = new[]
    {
        PublicWantedStatus.Entwurf,
        PublicWantedStatus.Beantragt,
        PublicWantedStatus.Veroeffentlicht,
        PublicWantedStatus.Gefasst,
        PublicWantedStatus.Zurueckgezogen,
        PublicWantedStatus.Abgelaufen,
    };
}
