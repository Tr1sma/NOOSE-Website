namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Verschlusssache-Stufe eines Bibliotheks-Dokuments bzw. einer Bibliotheks-Datei. Bestimmt, welcher
/// Personenkreis das Dokument sehen und bearbeiten darf. Die Stufen schließen sich gegenseitig aus
/// (genau eine ist gesetzt; <see cref="None"/> = offen für alle aktiven Agenten).
/// </summary>
public enum DocumentClassification
{
    /// <summary>Keine Verschlusssache – für alle aktiven Agenten sichtbar.</summary>
    None = 0,

    /// <summary>Verschlusssache nur für die Führung (Supervisory+/Admin sowie die Nur-Lese-Aufsicht).</summary>
    Leadership = 1,

    /// <summary>Verschlusssache nur für die Tactical Response Unit (TRU).</summary>
    Tru = 2,

    /// <summary>Verschlusssache nur für den Human Resources Branch (HRB).</summary>
    Hrb = 3,
}

/// <summary>Anzeige-Helfer für <see cref="DocumentClassification"/> (Label für Chips/Auswahllisten).</summary>
public static class DocumentClassificationDisplay
{
    /// <summary>Vollständige Bezeichnung der Stufe (z. B. „Verschlusssache nur für TRU").</summary>
    public static string Label(DocumentClassification classification) => classification switch
    {
        DocumentClassification.Leadership => "Verschlusssache nur für Führung",
        DocumentClassification.Tru => "Verschlusssache nur für TRU",
        DocumentClassification.Hrb => "Verschlusssache nur für HRB",
        _ => "Keine Verschlusssache",
    };

    /// <summary>Kurzes Chip-Label der Stufe (z. B. „VS – TRU"); leer bei <see cref="DocumentClassification.None"/>.</summary>
    public static string ChipLabel(DocumentClassification classification) => classification switch
    {
        DocumentClassification.Leadership => "Verschlusssache",
        DocumentClassification.Tru => "VS – TRU",
        DocumentClassification.Hrb => "VS – HRB",
        _ => string.Empty,
    };
}
