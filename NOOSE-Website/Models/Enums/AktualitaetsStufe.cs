using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Aktualitäts-Ampel einer Akte: Maß dafür, wie lange die letzte Änderung zurückliegt. Die Schwellwerte
/// (Tage bis „gelb"/„rot") sind je Aktentyp konfigurierbar (siehe <c>AktualitaetService</c>).
/// </summary>
public enum AktualitaetsStufe
{
    /// <summary>Aktuell (grün) – jünger als der Warnungs-Schwellwert.</summary>
    Frisch = 0,

    /// <summary>Wird älter (gelb) – zwischen Warnungs- und Veraltet-Schwellwert.</summary>
    Warnung = 1,

    /// <summary>Veraltet (rot) – älter als der Veraltet-Schwellwert.</summary>
    Veraltet = 2,
}

/// <summary>Berechnet die <see cref="AktualitaetsStufe"/> aus Schwellwerten und Referenzdatum (reine Funktion).</summary>
public static class AktualitaetsBewertung
{
    /// <summary>
    /// Bewertet eine Akte. <paramref name="referenzdatum"/> = <c>GeaendertAm ?? ErstelltAm</c>. „rot" wird vor
    /// „gelb" geprüft, damit eine (versehentliche) Schwellwert-Vertauschung nicht zu falsch-grünen Akten führt.
    /// </summary>
    public static AktualitaetsStufe Stufe(int warnungTage, int veraltetTage, DateTime referenzdatum, DateTime jetzt)
    {
        var alterTage = (jetzt - referenzdatum).TotalDays;
        if (alterTage >= veraltetTage)
        {
            return AktualitaetsStufe.Veraltet;
        }
        return alterTage >= warnungTage ? AktualitaetsStufe.Warnung : AktualitaetsStufe.Frisch;
    }
}

/// <summary>Anzeige-Helfer für die <see cref="AktualitaetsStufe"/> (Name + Ampelfarbe).</summary>
public static class AktualitaetsStufeAnzeige
{
    public static string Name(AktualitaetsStufe stufe) => stufe switch
    {
        AktualitaetsStufe.Frisch => "Aktuell",
        AktualitaetsStufe.Warnung => "Wird älter",
        AktualitaetsStufe.Veraltet => "Veraltet",
        _ => "Unbekannt",
    };

    public static Color Farbe(AktualitaetsStufe stufe) => stufe switch
    {
        AktualitaetsStufe.Frisch => Color.Success,
        AktualitaetsStufe.Warnung => Color.Warning,
        AktualitaetsStufe.Veraltet => Color.Error,
        _ => Color.Default,
    };
}
