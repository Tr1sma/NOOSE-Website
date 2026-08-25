namespace NOOSE_Website.Models.Enums;

/// <summary>Kind of public wanted notice.</summary>
/// <remarks>Complete from the start; only <see cref="Fahndung"/> is issued today, the rest arrive with later phases.</remarks>
public enum PublicWantedKind
{
    Fahndung = 0,
    Vermisst = 1,
    Zeugenaufruf = 2,
    Fahrzeug = 3,
    Waffe = 4,
}

/// <summary>Display labels.</summary>
public static class PublicWantedKindDisplay
{
    public static string Name(PublicWantedKind kind) => kind switch
    {
        PublicWantedKind.Fahndung => "Fahndung",
        PublicWantedKind.Vermisst => "Vermisst",
        PublicWantedKind.Zeugenaufruf => "Zeugenaufruf",
        PublicWantedKind.Fahrzeug => "Fahrzeug",
        PublicWantedKind.Waffe => "Waffe",
        _ => "—",
    };

    public static readonly IReadOnlyList<PublicWantedKind> All = new[]
    {
        PublicWantedKind.Fahndung,
        PublicWantedKind.Vermisst,
        PublicWantedKind.Zeugenaufruf,
        PublicWantedKind.Fahrzeug,
        PublicWantedKind.Waffe,
    };
}
