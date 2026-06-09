namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Typ eines Steckbrief-Vorschlags im gemeinsamen Vorschlagskatalog. Bestimmt, in welche „Schublade"
/// ein erfasster Wert gehört (Waffe/Fahrzeug/Ort) und welche Vorschläge das Autocomplete liefert.
/// </summary>
public enum VorschlagTyp
{
    Waffe = 0,
    Fahrzeug = 1,
    Ort = 2,
    // Phase 4: Lager-Bestände der Fraktionen (Waffen-Bestände nutzen weiterhin „Waffe").
    Lagerbestand = 3,
    // Phase 4: Art der Fraktion (Gang/Mafia/…) – Einzelwert mit Vorschlägen.
    Art = 4,
}
