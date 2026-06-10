namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Kategorie einer Personengruppen-Akte – was die Akte darstellt. Unabhängig von der
/// Sicherheits-<see cref="Einstufung"/> (Prüffall/Verdachtsfall/…).
/// </summary>
public enum GruppenArt
{
    Gruppierung = 0,
    Persoenlichkeit = 1,
    PersonOfInterest = 2,
}

/// <summary>Anzeigetexte für die Gruppen-Kategorie (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class GruppenArtAnzeige
{
    public static string Name(GruppenArt art) => art switch
    {
        GruppenArt.Gruppierung => "Gruppierung",
        GruppenArt.Persoenlichkeit => "Persönlichkeit",
        GruppenArt.PersonOfInterest => "Person of Interest",
        _ => "—",
    };

    public static readonly IReadOnlyList<GruppenArt> Alle = new[]
    {
        GruppenArt.Gruppierung,
        GruppenArt.Persoenlichkeit,
        GruppenArt.PersonOfInterest,
    };
}
