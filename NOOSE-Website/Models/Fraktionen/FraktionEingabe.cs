using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Models.Fraktionen;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Fraktion. Stammdaten + strukturierte
/// Listen (Ränge, Waffen-/Lagerbestand). Mitglieder werden NICHT hierüber gepflegt, sondern über
/// eigene Endpunkte (eigene Join-Tabelle mit Audit-Zeitstempel).
/// </summary>
public class FraktionEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Art { get; set; }
    public string? Funk { get; set; }
    public string? Darkchat { get; set; }
    public string? Ausstellungszeiten { get; set; }
    public string? Anwesen { get; set; }
    public string? Erkennungsfarbe { get; set; }
    public string? Ziele { get; set; }
    public string? Beschreibung { get; set; }
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public bool IstVerschlusssache { get; set; }

    /// <summary>Staatsfraktion: kann nicht „veraltet" werden (Aktualitäts-Ampel bleibt dauerhaft „Aktuell").</summary>
    public bool IstStaatsfraktion { get; set; }

    /// <summary>Geschätzte Gesamtgröße der Fraktion (= y im Erfassungsfortschritt x/y); optional.</summary>
    public int? GeschaetzteMitgliederzahl { get; set; }

    public List<RangEingabe> Raenge { get; set; } = new();
    public List<BestandEingabe> Waffenbestand { get; set; } = new();
    public List<BestandEingabe> Lagerbestand { get; set; } = new();

    /// <summary>Drogenrouten als generisches Mehrfachfeld; das Zusatzfeld (<see cref="BestandEingabe.Menge"/>) trägt hier die Notiz.</summary>
    public List<BestandEingabe> Drogenrouten { get; set; } = new();

    /// <summary>Mitglieder, die bereits beim Anlegen erfasst werden (auf der Detailseite weiter pflegbar).</summary>
    public List<MitgliedEingabe> Mitglieder { get; set; } = new();
}

/// <summary>Ein Rang-Eintrag im Bearbeiten-Formular (Bezeichnung; Sortierung folgt der Reihenfolge in der Liste).</summary>
public class RangEingabe
{
    public string Bezeichnung { get; set; } = string.Empty;
}

/// <summary>
/// Ein Bestands-Eintrag (Waffen-/Lagerbestand) als generisches Mehrfachfeld: Bezeichnung + optionale Menge.
/// </summary>
public class BestandEingabe : ISteckbriefMehrfach
{
    public string Bezeichnung { get; set; } = string.Empty;
    public string? Menge { get; set; }

    string ISteckbriefMehrfach.Hauptwert { get => Bezeichnung; set => Bezeichnung = value; }
    string? ISteckbriefMehrfach.Zusatz { get => Menge; set => Menge = value; }
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Fraktions-Mitgliedschaft.</summary>
public class MitgliedEingabe
{
    public string PersonId { get; set; } = string.Empty;
    public string? Rang { get; set; }
    public bool IstLeitung { get; set; }

    /// <summary>Nur für die Anzeige im Anlege-Formular; vom Dienst ignoriert.</summary>
    public string? PersonName { get; set; }

    /// <summary>
    /// Ist <see cref="PersonId"/> leer und dies gesetzt, wird beim Hinzufügen automatisch eine neue
    /// Personen-Akte mit diesem Namen angelegt und als Mitglied verknüpft.
    /// </summary>
    public string? NeuePersonName { get; set; }
}
