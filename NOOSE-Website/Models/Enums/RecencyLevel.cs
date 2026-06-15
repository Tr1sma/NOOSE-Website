using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Aktualitäts-Ampel einer Akte: Maß dafür, wie lange die letzte Änderung zurückliegt. Die Schwellwerte
/// (Tage bis „gelb"/„rot") sind je Aktentyp konfigurierbar (siehe <c>AktualitaetService</c>).
/// </summary>
public enum RecencyLevel
{
    /// <summary>Aktuell (grün) – jünger als der Warnungs-Schwellwert.</summary>
    Fresh = 0,

    /// <summary>Wird älter (gelb) – zwischen Warnungs- und Veraltet-Schwellwert.</summary>
    Warning = 1,

    /// <summary>Veraltet (rot) – älter als der Veraltet-Schwellwert.</summary>
    Stale = 2,
}

/// <summary>Berechnet die <see cref="AktualitaetsStufe"/> aus Schwellwerten und Referenzdatum (reine Funktion).</summary>
public static class RecencyAssessment
{
    /// <summary>
    /// Bewertet eine Akte. <paramref name="referenzdatum"/> = <c>GeaendertAm ?? ErstelltAm</c>. „rot" wird vor
    /// „gelb" geprüft, damit eine (versehentliche) Schwellwert-Vertauschung nicht zu falsch-grünen Akten führt.
    /// </summary>
    public static RecencyLevel Level(int warningDays, int staleDays, DateTime referenceDate, DateTime now)
    {
        var alterDays = (now - referenceDate).TotalDays;
        if (alterDays >= staleDays)
        {
            return RecencyLevel.Stale;
        }
        return alterDays >= warningDays ? RecencyLevel.Warning : RecencyLevel.Fresh;
    }
}

/// <summary>Anzeige-Helfer für die <see cref="AktualitaetsStufe"/> (Name + Ampelfarbe).</summary>
public static class RecencyLevelDisplay
{
    public static string Name(RecencyLevel level) => level switch
    {
        RecencyLevel.Fresh => "Aktuell",
        RecencyLevel.Warning => "Wird älter",
        RecencyLevel.Stale => "Veraltet",
        _ => "Unbekannt",
    };

    public static Color Colour(RecencyLevel level) => level switch
    {
        RecencyLevel.Fresh => Color.Success,
        RecencyLevel.Warning => Color.Warning,
        RecencyLevel.Stale => Color.Error,
        _ => Color.Default,
    };
}
