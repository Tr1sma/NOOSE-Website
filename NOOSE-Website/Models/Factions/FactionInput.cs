using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Models.Factions;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Fraktion. Stammdaten + strukturierte
/// Listen (Ränge, Waffen-/Lagerbestand). Mitglieder werden NICHT hierüber gepflegt, sondern über
/// eigene Endpunkte (eigene Join-Tabelle mit Audit-Zeitstempel).
/// </summary>
public class FactionInput
{
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? Radio { get; set; }
    public string? Darkchat { get; set; }
    public string? IssuingTimes { get; set; }
    public string? Estate { get; set; }
    public string? RecognitionColor { get; set; }
    public string? Targets { get; set; }
    public string? Description { get; set; }
    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public bool IsClassified { get; set; }

    /// <summary>Staatsfraktion: kann nicht „veraltet" werden (Aktualitäts-Ampel bleibt dauerhaft „Aktuell").</summary>
    public bool IsStateFaction { get; set; }

    /// <summary>Geschätzte Gesamtgröße der Fraktion (= y im Erfassungsfortschritt x/y); optional.</summary>
    public int? EstimatedMemberCount { get; set; }

    public List<RankInput> Ranks { get; set; } = new();
    public List<StockInput> WeaponStock { get; set; } = new();
    public List<StockInput> Inventory { get; set; } = new();

    /// <summary>Drogenrouten als generisches Mehrfachfeld; das Zusatzfeld (<see cref="BestandEingabe.Menge"/>) trägt hier die Notiz.</summary>
    public List<StockInput> DrugRoutes { get; set; } = new();

    /// <summary>Mitglieder, die bereits beim Anlegen erfasst werden (auf der Detailseite weiter pflegbar).</summary>
    public List<MemberInput> Members { get; set; } = new();
}

/// <summary>Ein Rang-Eintrag im Bearbeiten-Formular (Bezeichnung; Sortierung folgt der Reihenfolge in der Liste).</summary>
public class RankInput
{
    /// <summary>
    /// Id des bestehenden Rangs (leer bei neu hinzugefügten Rängen). Dient ausschließlich der Umbenennungs-Erkennung
    /// beim Speichern, damit der denormalisierte Rang-Name in der Mitgliederliste mitgezogen werden kann.
    /// </summary>
    public string? Id { get; set; }
    public string Designation { get; set; } = string.Empty;
}

/// <summary>
/// Ein Bestands-Eintrag (Waffen-/Lagerbestand) als generisches Mehrfachfeld: Bezeichnung + optionale Menge.
/// </summary>
public class StockInput : IProfileMultiple
{
    public string Designation { get; set; } = string.Empty;
    public string? Quantity { get; set; }

    string IProfileMultiple.MainValue { get => Designation; set => Designation = value; }
    string? IProfileMultiple.Extra { get => Quantity; set => Quantity = value; }
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Fraktions-Mitgliedschaft.</summary>
public class MemberInput
{
    public string PersonId { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public bool IsLead { get; set; }

    /// <summary>Nur für die Anzeige im Anlege-Formular; vom Dienst ignoriert.</summary>
    public string? PersonName { get; set; }

    /// <summary>
    /// Ist <see cref="PersonId"/> leer und dies gesetzt, wird beim Hinzufügen automatisch eine neue
    /// Personen-Akte mit diesem Namen angelegt und als Mitglied verknüpft.
    /// </summary>
    public string? NewPersonName { get; set; }
}
