namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Typ eines Steckbrief-Vorschlags im gemeinsamen Vorschlagskatalog. Bestimmt, in welche „Schublade"
/// ein erfasster Wert gehört (Waffe/Fahrzeug/Ort) und welche Vorschläge das Autocomplete liefert.
/// </summary>
public enum SuggestionType
{
    Weapon = 0,
    Vehicle = 1,
    Location = 2,
    // Phase 4: Lager-Bestände der Fraktionen (Waffen-Bestände nutzen weiterhin „Waffe").
    Inventory = 3,
    // Phase 4: Art der Fraktion (Gang/Mafia/…) – Einzelwert mit Vorschlägen.
    Kind = 4,
    // Phase 5a: Rolle eines Partei-Mitglieds (Vorsitz/Sprecher/…) – Einzelwert mit Vorschlägen.
    PartyRole = 5,
    // Phase 5b: Typ/Kategorie einer Operation (Razzia/Observation/Infiltration/…) – Einzelwert mit Vorschlägen.
    OperationType = 6,
    // Phase 5: Typ/Kategorie einer Vorgangs-/Fallakte (Ermittlung/Überwachung/Anschlag/…) – Einzelwert mit Vorschlägen.
    CaseType = 7,
    // Drogenrouten einer Fraktion (analog Waffen-/Lagerbestand) – Bezeichnung mit Vorschlägen.
    DrugRoute = 8,
}
