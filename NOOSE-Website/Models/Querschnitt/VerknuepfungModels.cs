namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Eingabe zum Anlegen einer generischen Verknüpfung auf eine beliebige Ziel-Akte.</summary>
public class VerknuepfungEingabe
{
    /// <summary>Aktentyp des Ziels (z. B. „Person", „Fraktion", „Agent", „PersonDok"). Standard: Person.</summary>
    public string ZielTyp { get; set; } = "Person";
    public string ZielId { get; set; } = string.Empty;
    public string? Label { get; set; }

    /// <summary>Nur-UI: lesbarer Anzeigename des Ziels (vom Picker gesetzt, nicht persistiert).</summary>
    public string? Anzeige { get; set; }
}

/// <summary>Aufbereitete Verknüpfung aus Sicht einer Akte: die jeweils „andere Seite" samt Bezeichnung.</summary>
/// <param name="Href">Navigationsziel der anderen Seite (z. B. „/personen/{id}") oder null, wenn nicht navigierbar (z. B. Agent).</param>
public record VerknuepfungAnzeige(
    string VerknuepfungId,
    string AndereTyp,
    string AndereId,
    string? Label,
    string AndereBezeichnung,
    bool Automatisch = false,
    string? Href = null);
